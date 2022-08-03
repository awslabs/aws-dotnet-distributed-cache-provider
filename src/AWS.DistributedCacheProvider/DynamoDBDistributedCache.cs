// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using Amazon.DynamoDBv2;
using Microsoft.Extensions.Caching.Distributed;
using Amazon.DynamoDBv2.Model;
using AWS.DistributedCacheProvider.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace AWS.DistributedCacheProvider
{
    public class DynamoDBDistributedCache : IDistributedCache
    {
        private readonly IAmazonDynamoDB _ddbClient;
        private readonly IDynamoDBTableCreator _dynamodbTableCreator;
        private bool _started;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

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

        private readonly ILogger<DynamoDBDistributedCache> _logger;

        /// <summary>
        /// Constructor for DynamoDBDistributedCache.
        /// </summary>
        /// <param name="client">DynamoDB Client</param>
        /// <param name="creator">Creator class that is responsible for creating or validating the DynamoDB Table</param>
        /// <param name="options">Configurable options for the cache</param>
        /// <exception cref="ArgumentNullException"></exception>
        public DynamoDBDistributedCache(IAmazonDynamoDB client, IDynamoDBTableCreator creator, DynamoDBDistributedCacheOptions options, ILoggerFactory? loggerFactory = null)
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
            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<DynamoDBDistributedCache>();
            }
            else
            {
                _logger = NullLoggerFactory.Instance.CreateLogger<DynamoDBDistributedCache>();
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
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        private async Task StartupAsync()
        {
            if(!_started)
            {
                _logger.LogDebug("Cache has not started yet. Attempting startup");
                await _semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    _logger.LogDebug("Passed Semaphore into critical section");
                    if (!_started)
                    {
                        _logger.LogDebug("Started still set to false, Starting up.");
                        await _dynamodbTableCreator.CreateTableIfNotExistsAsync(_ddbClient, _tableName, _createTableifNotExists, _ttlAttributeName);
                        _ttlAttributeName = await _dynamodbTableCreator.GetTTLColumnAsync(_ddbClient, _tableName);
                        _started = true;
                    }
                    else
                    {
                        _logger.LogDebug("Started was set to true, a different thread already started the cache");
                    }
                }
                finally
                {
                    _logger.LogDebug("Releasing sempahore");
                    _semaphore.Release();
                }
            }
        }

        /// <summary>
        ///<inheritdoc />
        ///<see href="https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/howitworks-ttl.html">DynamoDB's TTL policy</see>
        ///states that it can take items up to 48 hours to be deleted when they expire.
        ///As such, if an item's TTL has expired, but still happens to be on the table, this will still return null.
        /// </summary>
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        public byte[]? Get(string key)
        {
            var byteArray = GetAsync(key, new CancellationToken()).GetAwaiter().GetResult();
            return byteArray;
        }
        /// <summary>
        ///<inheritdoc />
        ///<see href="https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/howitworks-ttl.html">DynamoDB's TTL policy</see>
        ///states that it can take items up to 48 hours to be deleted when they expire.
        ///As such, if an item's TTL has expired, but still happens to be on the table, this will still return null.
        /// </summary>
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            _logger.LogDebug($"GetAsync called with key {key}");
            await StartupAsync();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            var getItemRequest = CreateGetItemRequest(key);
            GetItemResponse getItemResponse;
            try
            {
                _logger.LogDebug("Making GetItemSync call to DynamoDB");
                getItemResponse = await _ddbClient.GetItemAsync(getItemRequest, token);
            }
            catch(Exception e)
            {
                if (e is ResourceNotFoundException)
                {
                    _logger.LogDebug($"DynamoDB did not find an Item associated with the key {key}. Returning null");
                    return null;
                }
                throw new DynamoDBDistributedCacheException($"Failed to get Item with key {key}. Caused by {e.Message}", e);
            }
            if (getItemResponse.Item.ContainsKey(VALUE_KEY))
            {
                //Check even if the Item is present, but its TTL has expired. DynamoDB can take up to 48 hours to remove expired items
                if (getItemResponse.Item[TTL_DATE].N != null &&
                    DateTimeOffset.UtcNow.CompareTo(UnixSecondsToDateTimeOffset(getItemResponse.Item[TTL_DATE].N)) > 0)
                {
                    _logger.LogDebug("Response from DynamoDB did contain a value, but the TTL of the item has passed. Returning null");
                    return null;
                }
                var returnArray = getItemResponse.Item[VALUE_KEY].B.ToArray();
                _logger.LogDebug($"Returning response from DynamoDB. Byte array has length {returnArray.Length}");
                return returnArray;
            }
            else
            {
                _logger.LogDebug("Response from DynamoDB was an Item that did not contain a value in the value column. Returning null");
                return null;
            }
        }

        ///<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        public void Refresh(string key)
        {
            RefreshAsync(key, new CancellationToken()).GetAwaiter().GetResult();
        }

        ///<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            _logger.LogDebug($"RefreshAsync called with key {key}");
            await StartupAsync();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            var getItemRequest = CreateGetItemRequest(key);
            GetItemResponse getItemResponse;
            try
            {
                _logger.LogDebug("Making GetItemSync call to DynamoDB");
                getItemResponse = await _ddbClient.GetItemAsync(getItemRequest, token);
            }
            catch (Exception e)
            {
                throw new DynamoDBDistributedCacheException($"Failed to get item with key {key}. Caused by {e.Message}", e);
            }
            if (getItemResponse.Item[TTL_WINDOW].S != null)
            {
                var currentTtl = UnixSecondsToDateTimeOffset(getItemResponse.Item[TTL_DATE].N);
                var options = new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.Parse(getItemResponse.Item[TTL_WINDOW].S),
                };
                if (getItemResponse.Item[TTL_DEADLINE].N != null)
                {
                    options.AbsoluteExpiration = UnixSecondsToDateTimeOffset(getItemResponse.Item[TTL_DEADLINE].N);
                }
                var ttlAttribute = DynamoDBCacheProviderHelper.CalculateTTL(options);
                var newTtl = UnixSecondsToDateTimeOffset(ttlAttribute.N);
                var updateItemRequest = new UpdateItemRequest
                {
                    TableName = _tableName,
                    Key = CreateDictionaryWithPrimaryKey(key),
                    AttributeUpdates = new Dictionary<string, AttributeValueUpdate>
                    {
                        //On refresh we only move the TTL_DATE. the TTL_WINDOW and TTL_DEADLINE stay the same.
                        {
                            TTL_DATE, new AttributeValueUpdate
                            {
                                Value = ttlAttribute
                            }
                        }
                    }
                };
                try
                {
                    _logger.LogDebug($"Making UpdateItemAsync call to DynamoDB. TTL was {currentTtl} and is now {newTtl}");
                    await _ddbClient.UpdateItemAsync(updateItemRequest, token);
                }
                catch(Exception e)
                {
                    throw new DynamoDBDistributedCacheException($"Failed to refresh the TTL for the cache item: {e.Message}", e);
                }
            }
            else
            {
                _logger.LogDebug("Item response from DynamoDB did not contain a value in the TTL column.");
            }
        }

        ///<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        public void Remove(string key)
        {
            RemoveAsync(key, new CancellationToken()).GetAwaiter().GetResult();
        }

        ///<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            _logger.LogDebug($"RemoveAsync called with key {key}");
            await StartupAsync();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            var deleteItemRequest = CreateDeleteItemRequest(key);
            try
            {
                _logger.LogDebug("Making DeleteItemAsync call to DynamoDB");
                await _ddbClient.DeleteItemAsync(deleteItemRequest, token);
            }
            catch (Exception e)
            {
                throw new DynamoDBDistributedCacheException($"Failed to delete item with key {key}, Caused by {e.Message}", e);
            }
        }

        ///<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            SetAsync(key, value, options, new CancellationToken()).GetAwaiter().GetResult();
        }

        ///<inheritdoc />
        /// <exception cref="DynamoDBDistributedCacheException"> When the underlying requests to DynamoDB fail</exception>
        /// <exception cref="InvalidTableException"> When the table being used is invalid to be used as a cache</exception>"
        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            _logger.LogDebug("SetAsync called with key {key}, options {options}", key, options);
            await StartupAsync();
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
            var ttlDate = DynamoDBCacheProviderHelper.CalculateTTL(options);
            var ttlWindow = DynamoDBCacheProviderHelper.CalculateSlidingWindow(options);
            var ttlDeadline = DynamoDBCacheProviderHelper.CalculateTTLDeadline(options);
            var putItemRequest = new PutItemRequest
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
                        TTL_DATE, ttlDate
                    },
                    {
                        TTL_WINDOW, ttlWindow
                    },
                    {
                        TTL_DEADLINE, ttlDeadline
                    }
                }
            };
            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var logStringBuilder = new StringBuilder();
                    logStringBuilder.Append("Making a PutItemAsync call to DynamoDB. ");
                    if (ttlDate.N != null)
                    {
                        logStringBuilder.Append($"TTL for item is {UnixSecondsToDateTimeOffset(ttlDate.N)}. ");
                    }
                    else
                    {
                        logStringBuilder.Append("TTL for item is undefined. ");
                    }
                    if (ttlDeadline.N != null)
                    {
                        logStringBuilder.Append($"TTL Deadline for item is {UnixSecondsToDateTimeOffset(ttlDeadline.N)}. ");
                    }
                    else
                    {
                        logStringBuilder.Append("TTL Deadline for item is undefined. ");
                    }
                    if (ttlWindow.S != null)
                    {
                        logStringBuilder.Append($"TTL Window is {TimeSpan.Parse(ttlWindow.S)}. ");
                    }
                    else
                    {
                        logStringBuilder.Append("TTL Window is undefined. ");
                    }
                    _logger.LogDebug(logStringBuilder.ToString());
                }
                await _ddbClient.PutItemAsync(putItemRequest, token);
            }
            catch (Exception e)
            {
                throw new DynamoDBDistributedCacheException($"Failed to put item with key {key}. Caused by {e.Message}", e);
            }
        }

        /// <summary>
        /// Creates a <see cref="DeleteItemRequest"/> based on the <paramref name="key"/>. The TableName is stored as a field of this class
        /// </summary>
        /// <param name="key">The primary key for the request</param>
        private DeleteItemRequest CreateDeleteItemRequest(string key)
        {
            return new DeleteItemRequest
            {
                TableName = _tableName,
                Key = CreateDictionaryWithPrimaryKey(key)
            };
        }

        /// <summary>
        /// Creates a <see cref="GetItemRequest"/> based on the <paramref name="key"/>. The TableName and ConsistentReads are stored as fields of this class.
        /// </summary>
        /// <param name="key">The primary key for the request</param>
        private GetItemRequest CreateGetItemRequest(string key)
        {
            return new GetItemRequest
            {
                TableName = _tableName,
                Key = CreateDictionaryWithPrimaryKey(key),
                ConsistentRead = _consistentReads
            };
        }

        /// <summary>
        /// Creates a Dictionary of string to AttributeValue where the only entry is a pair mapping of the PRIMARY_KEY attribute value to the <paramref name="key"/>
        /// </summary>
        /// <param name="key">The primary key value</param>
        private Dictionary<string, AttributeValue> CreateDictionaryWithPrimaryKey(string key)
        {
            return new Dictionary<string, AttributeValue>()
            {
                {
                    PRIMARY_KEY, new AttributeValue {S = key }
                }
            };
        }

        /// <summary>
        /// Simple helper method that converts a Unix timestamp in seconds to a DateTimeOffset object.
        /// </summary>
        /// <param name="seconds">The Unix time stamp in seconds</param>
        /// <returns>The equivalent DateTimeOffset representation of the <paramref name="seconds"/></returns>
        private DateTimeOffset UnixSecondsToDateTimeOffset(string seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)double.Parse(seconds));
        }
    }
}
