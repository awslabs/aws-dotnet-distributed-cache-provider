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

        //configurable values
        private string _tableName { get; }
        private readonly bool _consistentReads;
        private string? _ttlAttributeName;
        private readonly bool _createTableifNotExists;

        //Const values for columns
        public const string PRIMARY_KEY = "primary_key";//column that the key for the entry is stored
        public const string DEFAULT_TTL_ATTRIBUTE_NAME = "expdate";
        private const string VALUE_KEY = "value_key";
        private const string TTL_DATE = "ttl_date";
        private const string TTL_WINDOW = "ttl_window";
        private const string TTL_DEADLINE = "ttl_deadline";

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
            _ttlAttributeName = options.TTLAttributeName;
            _createTableifNotExists = options.CreateTableIfNotExists;
        }

        /// <summary>
        /// Make sure the backing datastore is up and running before accepting client requests
        /// </summary>
        private async Task StartupAsync()
        {
            //future PR. Make this Thread Safe
            if(!_started)
            {
                System.Threading.Monitor.Enter(this);
                try
                {
                    if (!_started)
                    {
                        //future PR. This should reduced to a single method call. If table is being created, no need for TTL describe...
                        await _dynamodbTableCreator.CreateTableIfNotExistsAsync(_ddbClient, _tableName, _createTableifNotExists, _ttlAttributeName);
                        _ttlAttributeName = await _dynamodbTableCreator.GetTTLColumnAsync(_ddbClient, _tableName);
                        _started = true;
                    }
                }
                finally
                {
                    System.Threading.Monitor.Exit(this);
                }
            }
        }
        
        //<inheritdoc />
        public byte[]? Get(string key)
        {
            return GetAsync(key, new CancellationToken()).GetAwaiter().GetResult();
        }

        //<inheritdoc />
        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            await StartupAsync();
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
            var resp = await _ddbClient.GetItemAsync(getRequest, token);
            if (resp.Item.ContainsKey(VALUE_KEY))
            {
                //Do we check if TTL has expired but is still present on the table?
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
        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            await StartupAsync();
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
            var task = _ddbClient.GetItemAsync(getRequest, token);
            var task2 = task.ContinueWith(async t =>
            {
                var response = t.Result.Item;
                if (response[TTL_DATE] != null && response[TTL_WINDOW] != null && response[TTL_DEADLINE] != null)
                {
                    var ttl = (long)Convert.ToDouble(response[TTL_DATE].N);
                    var window = TimeSpan.Parse(response[TTL_WINDOW].N);
                    var absoluteTTL = (long)Convert.ToDouble(response[TTL_DEADLINE].N);
                    var baseTTl = DateTimeOffset.UtcNow;
                    //new ttl is min(now+window, deadline)
                    var ttlFromBase = baseTTl.Add(window).ToUnixTimeSeconds();
                    long calculatedTTL;
                    if (ttlFromBase < absoluteTTL)
                    {
                        calculatedTTL = ttlFromBase;
                    }
                    else
                    {
                        calculatedTTL = absoluteTTL;
                    }
                    //take new ttl and rewrite the item back to dynamodb
                    var options = new DistributedCacheEntryOptions()
                    {
                        AbsoluteExpiration = DateTimeOffset.FromUnixTimeSeconds(calculatedTTL),
                        SlidingExpiration = window
                    };
                    await SetAsync(key, response[VALUE_KEY].B.ToArray(), options, token);
                }
            });
            task2.Wait(token);
        }

        //<inheritdoc />
        public void Remove(string key)
        {
            RemoveAsync(key, new CancellationToken()).GetAwaiter().GetResult();
        }

        //<inheritdoc />
        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            await StartupAsync();
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
            await _ddbClient.DeleteItemAsync(deleteRequest, token);
        }

        //<inheritdoc />
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            SetAsync(key, value, options, new CancellationToken()).GetAwaiter().GetResult();
        }

        //<inheritdoc />
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            StartupAsync().GetAwaiter().GetResult();
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
            return _ddbClient.PutItemAsync(request, token);
        }

        /// <summary>
        /// Caclulates the absolute Time To Live (TTL) given the <paramref name="options"/>
        /// </summary>
        /// <param name="options"></param>
        /// <returns>An <see cref="AttributeValue"/> which contains either the absolute deadline TTL or nothing.</returns>
        /// <exception cref="Exception">When the caclualted absolute deadline is in the past.</exception>
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
                if (now.CompareTo(ttl) < 0)//if ttl is before current time
                {
                    throw new Exception("AbsoluteExpiration cannot be before now");
                }
                else
                {
                    return new AttributeValue { N = "" + ttl.ToUnixTimeSeconds() };
                }
            }
            else if (options.AbsoluteExpiration == null && options.AbsoluteExpirationRelativeToNow != null)
            {
                var ttl = DateTimeOffset.UtcNow.Add((TimeSpan)options.AbsoluteExpirationRelativeToNow).ToUnixTimeSeconds();
                return new AttributeValue { N = "" + ttl };
            }
            else //Both properties are not null. Default to AbsoluteExpirationRelativeToNow
            {
                var ttl = DateTimeOffset.UtcNow.Add((TimeSpan)options.AbsoluteExpirationRelativeToNow).ToUnixTimeSeconds();
                return new AttributeValue { N = "" + ttl };
            }
        }

        /// <summary>
        /// Caclulates the TTL.
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
    }
}
