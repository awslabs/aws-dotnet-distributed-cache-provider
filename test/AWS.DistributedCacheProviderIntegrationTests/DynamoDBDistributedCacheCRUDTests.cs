// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AWS.DistributedCacheProvider;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AWS.DistributedCacheProviderIntegrationTests
{
    public class DynamoDBDistributedCacheCRUDTests : IClassFixture<DynamoDBDistributedCacheCRUDTests.CacheFixture>
    {
        public static readonly string _tableName = "AWS.DistributedCacheProviderIntegrationTests_CRUD_Tests";
        private readonly AmazonDynamoDBClient _client = new();
        private readonly IDistributedCache _cache;

        public DynamoDBDistributedCacheCRUDTests(CacheFixture fixture)
        {
            _cache = fixture._cache;
        }

        [Fact]
        public void Get_KeyReturnsValue_NoTTL()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            ManualPut(key, value, new AttributeValue { NULL = true },
                new AttributeValue { NULL = true }, new AttributeValue { NULL = true });
            var response = _cache.Get(key);
            Assert.Equal(response, value);
        }

        [Fact]
        public void Get_ExiredItemShouldReturnNull()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            var expiredTTL = DateTimeOffset.UtcNow.AddHours(-5).ToUnixTimeSeconds();
            ManualPut(key, value, new AttributeValue { N = expiredTTL.ToString() },
                new AttributeValue { NULL = true }, new AttributeValue { NULL = true });
            Assert.Null(_cache.Get(key));
        }

        [Fact]
        public void Remove_KeyReturnsNull()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            ManualPut(key, value, new AttributeValue { NULL = true },
                new AttributeValue { NULL = true }, new AttributeValue { NULL = true });
            _cache.Remove(key);
            var response = _cache.Get(key);
            Assert.Null(response);
        }

        [Fact]
        public void SetAndGet()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            _cache.Set(key, value, new DistributedCacheEntryOptions());
            var resp = _cache.Get(key);
            Assert.Equal(value, resp);
        }

        /*Tests that relate to calculating different TTL attributes*/
        [Fact]
        public async void Set_NullTTLOptions()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            _cache.Set(key, value, new DistributedCacheEntryOptions());
            var resp = await GetItemAsync(key);
            Assert.True(resp.Item[DynamoDBDistributedCache.TTL_WINDOW].NULL);
            Assert.True(resp.Item[DynamoDBDistributedCache.TTL_DEADLINE].NULL);
            Assert.True(resp.Item[DynamoDBDistributedCache.TTL_DATE].NULL);
        }

        [Fact]
        public async void Set_OnlyWindowOptionSet_TTLWithNoDeadline()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            var window = new TimeSpan(12, 0, 0);
            _cache.Set(key, value, new DistributedCacheEntryOptions
            {
                SlidingExpiration = window
            });
            var resp = (await GetItemAsync(key)).Item;
            //window is 12 hours
            Assert.Equal(TimeSpan.Parse(resp[DynamoDBDistributedCache.TTL_WINDOW].S), window);
            Assert.True(resp[DynamoDBDistributedCache.TTL_DEADLINE].NULL);
            //ttl date is approx 12 hours from now
            Assert.True(
                Math.Abs(
                    double.Parse(resp[DynamoDBDistributedCache.TTL_DATE].N) - DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds())
                < 100);
        }

        [Fact]
        public async void Set_OnlyRelativeOptionSet_DeadlineAndTTLSet()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            var ttl = new TimeSpan(12, 0, 0);
            var ttlInUnix = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
            _cache.Set(key, value, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            });
            var resp = (await GetItemAsync(key)).Item;
            Assert.True(resp[DynamoDBDistributedCache.TTL_WINDOW].NULL);
            Assert.Equal(resp[DynamoDBDistributedCache.TTL_DEADLINE].N, resp[DynamoDBDistributedCache.TTL_DATE].N);
            //Can't guarantee how close they will be, but within 100 seconds seems more than generous.
            Assert.True(Math.Abs(double.Parse(resp[DynamoDBDistributedCache.TTL_DATE].N) - ttlInUnix) < 100);
        }

        [Fact]
        public async void Set_RelativeAndWindow_AllSet()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            var deadline = new TimeSpan(24, 0, 0);
            var window = new TimeSpan(12, 0, 0);
            _cache.Set(key, value, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = deadline,
                SlidingExpiration = window
            });
            var resp = (await GetItemAsync(key)).Item;
            //window is 12 hours
            Assert.Equal(TimeSpan.Parse(resp[DynamoDBDistributedCache.TTL_WINDOW].S), window);
            //ttl date is approx 12 hours from now
            Assert.True(
                Math.Abs(
                    double.Parse(resp[DynamoDBDistributedCache.TTL_DATE].N) - DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds())
                < 100);
            //ttl deadline is approx 24 hours away
            Assert.True(
                Math.Abs(
                    double.Parse(resp[DynamoDBDistributedCache.TTL_DEADLINE].N) - DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds())
                < 100);
        }

        [Fact]
        public async void Set_OnlyAbsoluteOptionSet_DeadlineAndTTLSet()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            var deadline = DateTimeOffset.UtcNow.AddHours(12);
            var deadlineInUnix = deadline.ToUnixTimeSeconds();
            _cache.Set(key, value, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = deadline
            });
            var resp = (await GetItemAsync(key)).Item;
            Assert.True(resp[DynamoDBDistributedCache.TTL_WINDOW].NULL);
            Assert.Equal(resp[DynamoDBDistributedCache.TTL_DEADLINE].N, resp[DynamoDBDistributedCache.TTL_DATE].N);
            //Can't guarantee how close they will be, but within 100 seconds seems more than generous.
            Assert.True(Math.Abs(double.Parse(resp[DynamoDBDistributedCache.TTL_DATE].N) - deadlineInUnix) < 100);
        }

        [Fact]
        public async void Set_AbsoluteAndWindow_AllSet()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            var deadline = DateTimeOffset.UtcNow.AddHours(24);
            var deadlineInUnix = deadline.ToUnixTimeSeconds();
            var window = new TimeSpan(12, 0, 0);
            _cache.Set(key, value, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = deadline,
                SlidingExpiration = window
            });
            var resp = (await GetItemAsync(key)).Item;
            //window is 12 hours
            Assert.Equal(TimeSpan.Parse(resp[DynamoDBDistributedCache.TTL_WINDOW].S), window);
            //ttl date is approx 12 hours from now
            Assert.True(
                Math.Abs(
                    double.Parse(resp[DynamoDBDistributedCache.TTL_DATE].N) - DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds())
                < 100);
            //ttl deadline is approx 24 hours away
            Assert.True(
                Math.Abs(
                    double.Parse(resp[DynamoDBDistributedCache.TTL_DEADLINE].N) - DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds())
                < 100);
        }

        [Fact]
        public async void Set_AbsoluteAndRelativeSet_RelativeTakesPrecedence()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            var deadline = DateTimeOffset.UtcNow.AddHours(24);
            _cache.Set(key, value, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = deadline,
                AbsoluteExpirationRelativeToNow = new TimeSpan(12, 0, 0)
            });
            var resp = (await GetItemAsync(key)).Item;
            //Window is null
            Assert.True(resp[DynamoDBDistributedCache.TTL_WINDOW].NULL);
            //ttl date and deadline are equal
            Assert.Equal(resp[DynamoDBDistributedCache.TTL_DATE].N, resp[DynamoDBDistributedCache.TTL_DEADLINE].N);
            //ttl date is approx 12 hours from now
            Assert.True(
                Math.Abs(
                    double.Parse(resp[DynamoDBDistributedCache.TTL_DATE].N) - DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds())
                < 100);
        }

        [Fact]
        public async void Set_AllOptionsUsed()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            var deadline_abs = DateTimeOffset.UtcNow.AddHours(48);
            var deadline_rel = new TimeSpan(24, 0, 0);
            var window = new TimeSpan(12, 0, 0);
            _cache.Set(key, value, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = deadline_abs,
                AbsoluteExpirationRelativeToNow = deadline_rel,
                SlidingExpiration = window
            });
            var resp = (await GetItemAsync(key)).Item;
            //Window is 12 hours
            Assert.Equal(TimeSpan.Parse(resp[DynamoDBDistributedCache.TTL_WINDOW].S), window);
            //ttl date is approx 12 hours from now
            Assert.True(
                Math.Abs(
                    double.Parse(resp[DynamoDBDistributedCache.TTL_DATE].N) - DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds())
                < 100);
            //ttl deadline is approx 24 hours from now
            Assert.True(
                Math.Abs(
                    double.Parse(resp[DynamoDBDistributedCache.TTL_DEADLINE].N) - DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds())
                < 100);
        }

        //There are three columns related to TTL: TTL_DATE, TTL_DEADLINE, TTL_WINDOW. We need to test combinations of all 3
        [Fact]
        public async void Refresh_AllNullColumns()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            ManualPut(key, value, new AttributeValue { NULL = true },
                new AttributeValue { NULL = true }, new AttributeValue { NULL = true });
            _cache.Refresh(key);
            var resp = await GetItemAsync(key);
            Assert.True(resp.Item[DynamoDBDistributedCache.TTL_WINDOW].NULL);
            Assert.True(resp.Item[DynamoDBDistributedCache.TTL_DATE].NULL);
            Assert.True(resp.Item[DynamoDBDistributedCache.TTL_DEADLINE].NULL);
        }

        //If TTL_DATE is null, then the item never expires.
        //If TTL_DATE is not null, but TTL_WINDOW is, then TTL_DATE and TTL_DEADLINE should be equal.
        //Even if they are not, Refresh() is meaningless
        [Fact]
        public async void Refresh_TTLWindowIsNull_RefreshChangesNothing()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            //Set TTL_DATE and TTL_DEADLINE to 12 hours from now
            var ttl = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds().ToString();

            ManualPut(key, value, new AttributeValue { N = ttl }, new AttributeValue { N = ttl }, new AttributeValue { NULL = true });
            _cache.Refresh(key);
            var resp = await GetItemAsync(key);
            Assert.True(resp.Item[DynamoDBDistributedCache.TTL_WINDOW].NULL);
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_DATE].N, ttl);
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_DEADLINE].N, ttl);
        }

        [Fact]
        public async void Refresh_MoveTTLWithinDeadline()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            //Set TTL_DATE to 12 hours from now
            //Set TTL_DEADLINE to 48 hours from now
            //Set TTL_WINDOW to 24 hours
            var ttl_date = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds().ToString();
            var ttl_deadline = DateTimeOffset.UtcNow.AddHours(48).ToUnixTimeSeconds().ToString();
            var ttl_window = new TimeSpan(24, 0, 0).ToString();
            ManualPut(key, value, new AttributeValue { N = ttl_date },
                new AttributeValue { N = ttl_deadline }, new AttributeValue { S = ttl_window });
            _cache.Refresh(key);
            var resp = await GetItemAsync(key);
            //window stays the same
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_WINDOW].S, ttl_window);
            //deadline stays the same
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_DEADLINE].N, ttl_deadline);
            //refresh moved TTL_DATE to approx 24 hours from now
            Assert.True(
                Math.Abs(
                    double.Parse(resp.Item[DynamoDBDistributedCache.TTL_DATE].N) - DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()
                    ) < 100);
        }

        [Fact]
        public async void Refresh_MoveTTLToDeadline()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            //Set TTL_DATE to 6 hours from now
            //Set TTL_DEADLINE to 18 hours from now
            //Set TTL_WINDOW to 24 hours
            var ttl_date = DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds().ToString();
            var ttl_deadline = DateTimeOffset.UtcNow.AddHours(18).ToUnixTimeSeconds().ToString();
            var ttl_window = new TimeSpan(24, 0, 0).ToString();
            ManualPut(key, value, new AttributeValue { N = ttl_date },
                new AttributeValue { N = ttl_deadline }, new AttributeValue { S = ttl_window });
            _cache.Refresh(key);
            var resp = await GetItemAsync(key);
            //window stays the same
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_WINDOW].S, ttl_window);
            //refresh moves TTL_DATE to be equal to TTL_DEADLINE
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_DATE].N, resp.Item[DynamoDBDistributedCache.TTL_DEADLINE].N);
            //deadline stays the same
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_DEADLINE].N, ttl_deadline);
        }

        [Fact]
        public async void Get_Also_Refreshes()
        {
            var key = RandomString();
            var value = Encoding.ASCII.GetBytes(RandomString());
            //Set TTL_DATE to 6 hours from now
            //Set TTL_DEADLINE to 18 hours from now
            //Set TTL_WINDOW to 24 hours
            var ttl_date = DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds().ToString();
            var ttl_deadline = DateTimeOffset.UtcNow.AddHours(18).ToUnixTimeSeconds().ToString();
            var ttl_window = new TimeSpan(24, 0, 0).ToString();
            ManualPut(key, value, new AttributeValue { N = ttl_date },
                new AttributeValue { N = ttl_deadline }, new AttributeValue { S = ttl_window });
            //Instead of Refresh, call Get to have the same result
            _cache.Get(key);
            var resp = await GetItemAsync(key);
            //window stays the same
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_WINDOW].S, ttl_window);
            //refresh moves TTL_DATE to be equal to TTL_DEADLINE
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_DATE].N, resp.Item[DynamoDBDistributedCache.TTL_DEADLINE].N);
            //deadline stays the same
            Assert.Equal(resp.Item[DynamoDBDistributedCache.TTL_DEADLINE].N, ttl_deadline);
        }

        public static string RandomString()
        {
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private Task<GetItemResponse> GetItemAsync(string key)
        {
            return _client.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    {
                        DynamoDBDistributedCache.PRIMARY_KEY, new AttributeValue{S = key}
                    }
                }
            });
        }

        private void ManualPut(string key, byte[] value, AttributeValue ttl, AttributeValue deadline, AttributeValue window)
        {
            _client.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    {
                        DynamoDBDistributedCache.PRIMARY_KEY, new AttributeValue { S = key }
                    },
                    {
                        DynamoDBDistributedCache.VALUE_KEY, new AttributeValue {B = new MemoryStream(value)}
                    },
                    {
                        DynamoDBDistributedCache.TTL_DATE, ttl
                    },
                    {
                        DynamoDBDistributedCache.TTL_DEADLINE, deadline
                    },
                    {
                        DynamoDBDistributedCache.TTL_WINDOW, window
                    }
                }
            }).GetAwaiter().GetResult();
        }

        public class CacheFixture : IDisposable
        {
            public readonly DynamoDBDistributedCache _cache;
            private readonly AmazonDynamoDBClient _client = new();

            public CacheFixture()
            {
                var serviceContainer = new ServiceCollection();
                serviceContainer.AddAWSDynamoDBDistributedCache(options =>
                {
                    options.TableName = DynamoDBDistributedCacheCRUDTests._tableName;
                    options.CreateTableIfNotExists = true;
                });
                var provider = ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(serviceContainer);
                _cache = (DynamoDBDistributedCache)provider.GetService<IDistributedCache>()!;
            }

            public void Dispose()
            {
                var isActive = false;
                while (!isActive)
                {
                    var descRequest = new DescribeTableRequest
                    {
                        TableName = _tableName
                    };
                    var descResponse = _client.DescribeTableAsync(descRequest).GetAwaiter().GetResult();
                    var tableStatus = descResponse.Table.TableStatus;

                    if (tableStatus == TableStatus.ACTIVE)
                        isActive = true;
                }
                _client.DeleteTableAsync(_tableName).Wait();
                var exists = true;
                while (exists)
                {
                    var task = _client.ListTablesAsync();
                    var resp = task.Result;
                    if (!resp.TableNames.Contains(_tableName))
                    {
                        exists = false;
                    }
                }
            }
        }
    }
}
