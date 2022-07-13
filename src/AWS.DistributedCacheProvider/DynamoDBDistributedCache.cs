// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.Runtime.Internal.Util;
using Amazon.DynamoDBv2;
using Microsoft.Extensions.Caching.Distributed;
using Amazon.DynamoDBv2.Model;

namespace AWS.DistributedCacheProvider
{
    public class DynamoDBDistributedCache : IDistributedCache
    {
        private readonly IAmazonDynamoDB _ddbClient;
        private readonly IDynamoDBTableCreator _dynamodbTableCreator;
        private bool _started;
        private static readonly object loc = new ();

        //configurable values
        private string _tableName { get; }
        private readonly bool _consistentReads;
        private string _ttlAttributeName;
        private readonly bool _createTableifNotExists;

        //Const values for columns
        public const string PRIMARY_KEY = "primary_key";//column that the key for the entry is stored
        public const string DEFAULT_TTL_ATTRIBUTE_NAME = TTL_DATE;
        public const string VALUE_KEY = "value_key";
        public const string TTL_DATE = "ttl_date";//The column name that stores the Time To Live of an Item
        public const string TTL_WINDOW = "ttl_window";//How far in the future to push the TTL of an Item when Refresh() is called
        public const string TTL_DEADLINE = "ttl_deadline";//The limit of how far the TTL can be pushed in cases of Refresh()

        private static readonly ILogger _logger = Logger.GetLogger(typeof(DynamoDBDistributedCache));

        /// <summary>
        /// Constructor for DynamoDBDistributedCache.
        /// </summary>
        /// <param name="client">DynamoDB Client</param>
        /// <param name="creator">Creator class that is responsible for creating or validating the DynamoDB Table</param>
        /// <param name="options">Configurable options for the cache</param>
        /// <exception cref="ArgumentNullException"></exception>
        public DynamoDBDistributedCache(IAmazonDynamoDB client, IDynamoDBTableCreator creator, DynamoDBDistributedCacheOptions options)
        {
            if(client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if(options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (creator == null)
            {
                throw new ArgumentNullException(nameof(creator));
            }
            _ddbClient = client;
            _dynamodbTableCreator = creator;
            _consistentReads = options.ConsistentReads;
            _tableName = options.TableName;
            _ttlAttributeName = options.TTLAttributeName ?? DEFAULT_TTL_ATTRIBUTE_NAME;
            _createTableifNotExists = options.CreateTableIfNotExists;
        }

        /// <summary>
        /// Make sure the backing datastore is up and running before accepting client requests
        /// </summary>
        private void Startup()
        {
            if(!_started)
            {
                System.Threading.Monitor.Enter(loc);
                try
                {
                    if (!_started)
                    {
                        _dynamodbTableCreator.CreateTableIfNotExistsAsync(_ddbClient, _tableName, _createTableifNotExists, _ttlAttributeName).GetAwaiter().GetResult();
                        _ttlAttributeName = _dynamodbTableCreator.GetTTLColumnAsync(_ddbClient, _tableName).GetAwaiter().GetResult();
                        _started = true;
                    }
                }
                finally
                {
                    System.Threading.Monitor.Exit(loc);
                }
            }
        }

        /// <summary>
        ///<inheritdoc />
        ///DynamoDB's TTL policy is such that it can take items up to 48 hours to be deleted when they expire.
        ///As such, if an item's TTL has expired, but still happens to be on the table, this will still return null.
        /// </summary>
        public byte[]? Get(string key)
        {
            return GetAsync(key, new CancellationToken()).GetAwaiter().GetResult();
        }
        /// <summary>
        ///<inheritdoc />
        ///DynamoDB's TTL policy is such that it can take items up to 48 hours to be deleted when they expire.
        ///As such, if an item's TTL has expired, but still happens to be on the table, this will still return null.
        /// </summary>
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Startup();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            var getRequest = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>()
                {
                    {
                        PRIMARY_KEY, new AttributeValue { S = key }
                    }
                },
                ConsistentRead = _consistentReads
            };
            var resp = await ActAndHandleException<GetItemResponse>(async () =>
            {
                return await _ddbClient.GetItemAsync(getRequest, token);
            });
            if (resp.Item.ContainsKey(VALUE_KEY))
            {
                //Check even if the Item is present, but its TTL has expired. DynamoDB can take up to 48 hours to remove expired items
                if (resp.Item[TTL_DATE].N != null &&
                    DateTimeOffset.UtcNow.CompareTo(DateTimeOffset.FromUnixTimeSeconds((long)double.Parse(resp.Item[TTL_DATE].N))) > 0)
                {
                    return null;
                }
                return resp.Item[VALUE_KEY].B.ToArray();
            }
            else
            {
                return null;
            }
        }

        //<inheritdoc />
        public void Refresh(string key)
        {
            RefreshAsync(key, new CancellationToken()).GetAwaiter().GetResult();
        }

        //<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            Startup();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            var getRequest = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>()
                {
                    {
                        PRIMARY_KEY, new AttributeValue {S = key}
                    }
                },
                ConsistentRead = _consistentReads
            };
            var response = (await ActAndHandleException<GetItemResponse>(async () =>
            {
                return await _ddbClient.GetItemAsync(getRequest, token);
            })).Item;
            if (response[TTL_WINDOW].S != null)
            {
                var options = new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.Parse(response[TTL_WINDOW].S),
                };
                //My impression is that there is a way to simply this into one line in the DistributedCacheEntryOptions constructor
                if (response[TTL_DEADLINE].N != null)
                {
                    options.AbsoluteExpiration = DateTimeOffset.FromUnixTimeSeconds((long)Convert.ToDouble(response[TTL_DEADLINE].N));
                }
                var ttl = CalculateTTL(options);
                await ActAndHandleException<Task>(async () =>
                {
                    await _ddbClient.UpdateItemAsync(new UpdateItemRequest
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                    {
                        {
                            PRIMARY_KEY, new AttributeValue {S = key}
                        }
                    },
                        AttributeUpdates = new Dictionary<string, AttributeValueUpdate>
                    {
                        //On refresh we only move the TTL_DATE, the TTL_WINDOW and TTL_DEADLINE stay the same
                        {
                            TTL_DATE, new AttributeValueUpdate
                            {
                                Value = ttl
                            }
                        }
                    }
                    });
                    return Task.CompletedTask;
                });
            }
        }

        //<inheritdoc />
        public void Remove(string key)
        {
            RemoveAsync(key, new CancellationToken()).GetAwaiter().GetResult();
        }

        //<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            Startup();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            var deleteRequest = new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>()
                {
                    {
                        PRIMARY_KEY, new AttributeValue {S = key }
                    }
                }
            };
            await ActAndHandleException<Task>(async () =>
            {
                await _ddbClient.DeleteItemAsync(deleteRequest, token);
                return Task.CompletedTask;
            });
        }

        //<inheritdoc />
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            SetAsync(key, value, options, new CancellationToken()).GetAwaiter().GetResult();
        }

        //<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Startup();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            var ttl = CalculateTTL(options);
            var AbsoluteTtlDate = CalculateTTLDeadline(options);
            var ttlWindow = CalculateSlidingWindow(options);
            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>()
                {
                    {
                        PRIMARY_KEY, new AttributeValue{S = key}
                    },
                    {
                        VALUE_KEY, new AttributeValue{ B = new MemoryStream(value)}
                    },
                    {
                        TTL_DATE, ttl
                    },
                    {
                        TTL_WINDOW, ttlWindow
                    },
                    {
                        TTL_DEADLINE, AbsoluteTtlDate
                    }
                }
            };
            await ActAndHandleException<Task>(async () =>
            {
                await _ddbClient.PutItemAsync(request, token);
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Calculates the absolute Time To Live (TTL) given the <paramref name="options"/>
        /// </summary>
        /// <param name="options"></param>
        /// <returns>An <see cref="AttributeValue"/> which contains either the absolute deadline TTL or nothing.</returns>
        /// <exception cref="ArgumentOutOfRangeException">When the caclualted absolute deadline is in the past.</exception>
        private AttributeValue CalculateTTLDeadline(DistributedCacheEntryOptions options)
        {
            if (options.AbsoluteExpiration == null && options.AbsoluteExpirationRelativeToNow == null)
            {
                return new AttributeValue { NULL = true };
            }
            else if (options.AbsoluteExpiration != null && options.AbsoluteExpirationRelativeToNow == null)
            {
                var ttl = (DateTimeOffset)options.AbsoluteExpiration;
                var now = DateTimeOffset.UtcNow;
                if (now.CompareTo(ttl) > 0)//if ttl is before current time
                {
                     throw new ArgumentOutOfRangeException("AbsoluteExpiration must be in the future.");
                }
                else
                {
                    return new AttributeValue { N = "" + ttl.ToUnixTimeSeconds() };
                }
            }//AbsoluteExpirationRelativeToNow is not null, regardless of what AbsoluteExpiration is set to, we prefer AbsoluteExpirationRelativeToNow
            else
            {
                var ttl = DateTimeOffset.UtcNow.Add((TimeSpan)options.AbsoluteExpirationRelativeToNow!).ToUnixTimeSeconds();
                return new AttributeValue { N = "" + ttl };
            }
        }

        /// <summary>
        /// Calculates the TTL.
        /// </summary>
        /// <param name="options"></param>
        /// <returns>An <see cref="AttributeValue"/> containting the TTL</returns>
        private AttributeValue CalculateTTL(DistributedCacheEntryOptions options)
        {
            //if the sliding window is present, then now + window
            if (options.SlidingExpiration != null)
            {
                var ttl = DateTimeOffset.UtcNow.Add(((TimeSpan)options.SlidingExpiration));
                //Cannot be later than the deadline
                var absoluteTTL = CalculateTTLDeadline(options);
                if (absoluteTTL.NULL)
                {
                    return new AttributeValue { N = "" + ttl.ToUnixTimeSeconds() };
                }
                else //return smaller of the two. Either the TTL based on the sliding window or the deadline
                {
                    if (long.Parse(absoluteTTL.N) < ttl.ToUnixTimeSeconds())
                    {
                        return absoluteTTL;
                    }
                    else
                    {
                        return new AttributeValue { N = "" + ttl.ToUnixTimeSeconds() };
                    }
                }
            }
            else //just return the absolute TTL
            {
                return CalculateTTLDeadline(options);
            }
        }

        /// <summary>
        /// Returns the sliding window of the TTL
        /// </summary>
        /// <param name="options"></param>
        /// <returns>An <see cref="AttributeValue"/> which either contains a string version of the sliding window <see cref="TimeSpan"/>
        ///  or nothing</returns>
        private AttributeValue CalculateSlidingWindow(DistributedCacheEntryOptions options)
        {
            if (options.SlidingExpiration != null)
            {
                return new AttributeValue { S = options.SlidingExpiration.ToString() };
            }
            else
            {
                return new AttributeValue { NULL = true };
            }
        }

        /// <summary>
        /// Completes the <paramref name="action"/> and wraps any <see cref="AmazonDynamoDBException"/> in a <see cref="DynamoDBDistributedCacheException"/>
        /// </summary>
        /// <exception cref="DynamoDBDistributedCacheException"> When the <paramref name="action"/> throws a <see cref="DynamoDBDistributedCacheException"/></exception>
        private async Task<T> ActAndHandleException<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (AmazonDynamoDBException e)
            {
                throw new DynamoDBDistributedCacheException(e.Message, e);
            }
        }
    }
}
