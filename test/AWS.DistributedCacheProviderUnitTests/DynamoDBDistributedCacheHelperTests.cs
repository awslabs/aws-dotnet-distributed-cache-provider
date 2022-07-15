// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using AWS.DistributedCacheProvider.Internal;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

namespace AWS.DistributedCacheProviderUnitTests
{
    public class DynamoDBDistributedCacheHelperTests
    {
        /*CalculateSlidingWindow Tests*/
        [Fact]
        public void CalculateSlidingWindow_NoWindow_NullAttributeReturn()
        {
            var ret = DynamoDBCacheProviderHelper.CalculateSlidingWindow(new DistributedCacheEntryOptions());
            Assert.True(ret.NULL);
        }

        [Fact]
        public void CalculateSlidingWindow_YesWindow_ReturnSerializedSlidingWindow()
        {
            var window = new TimeSpan(12, 30, 21);
            var ret = DynamoDBCacheProviderHelper.CalculateSlidingWindow(new DistributedCacheEntryOptions
            {
                SlidingExpiration = window
            });
            Assert.Equal(window, TimeSpan.Parse(ret.S));
        }

        /*CalculateTTL Tests*/
        [Fact]
        public void CalculateTTL_NoSlidingWindow_ReturnDeadline()
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = new TimeSpan(12, 31, 2)
            };
            var deadline = DynamoDBCacheProviderHelper.CalculateTTLDeadline(options);
            var ttl = DynamoDBCacheProviderHelper.CalculateTTL(options);
            Assert.False(deadline.NULL);
            Assert.False(ttl.NULL);
            Assert.Equal(ttl.N, deadline.N);
        }

        [Fact]
        public void CalculateTTL_NoDeadlineYesWindow_ReturnNowPlusWindow()
        {
            var window = new TimeSpan(12, 31, 2);
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = window
            };
            var ret = DynamoDBCacheProviderHelper.CalculateTTL(options);
            Assert.True(
                Math.Abs(DateTimeOffset.UtcNow.Add(window).ToUnixTimeSeconds() - double.Parse(ret.N))
                < 100);
        }

        [Fact]
        public void CalculateTTL_YesDeadlineYesWindow_ReturnAtDeadline()
        {
            var hoursToDeadline = 9;
            var window = new TimeSpan(12, 0, 0);
            var deadline = new TimeSpan(hoursToDeadline, 0, 0);
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = window,
                AbsoluteExpirationRelativeToNow = deadline
            };
            var ret = DynamoDBCacheProviderHelper.CalculateTTL(options);
            //ttl should be only 9 hours from now, not 12
            Assert.True(
                Math.Abs(DateTimeOffset.UtcNow.AddHours(hoursToDeadline).ToUnixTimeSeconds() - double.Parse(ret.N))
                < 100);
        }

        [Fact]
        public void CalculateTTL_YesDeadlineYesWindow_ReturnWithinDeadline()
        {
            var hoursToDeadline = 24;
            var hoursToWindow = 12;
            var window = new TimeSpan(hoursToWindow, 0, 0);
            var deadline = new TimeSpan(hoursToDeadline, 0, 0);
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = window,
                AbsoluteExpirationRelativeToNow = deadline
            };
            var ret = DynamoDBCacheProviderHelper.CalculateTTL(options);
            //ttl should be only 12 hours from now, not 24
            Assert.True(
                Math.Abs(DateTimeOffset.UtcNow.AddHours(hoursToWindow).ToUnixTimeSeconds() - double.Parse(ret.N))
                < 100);
        }

        /*CalculateTTLDeadline Tests*/
        [Fact]
        public void CalculateTTLDeadline_NullOptions_ReturnNullAttribute()
        {
            Assert.True(DynamoDBCacheProviderHelper.CalculateTTLDeadline(new DistributedCacheEntryOptions()).NULL);
        }

        [Fact]
        public void CalculateTTLDeadline_BothOptions_PreferRelativeOption()
        {
            var relative = new TimeSpan(12, 0, 0);
            var Absolute = DateTimeOffset.UtcNow.AddHours(24);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = Absolute,
                AbsoluteExpirationRelativeToNow = relative
            };
            var ret = DynamoDBCacheProviderHelper.CalculateTTLDeadline(options);
            //assert the deadline is approx 12 hours from now, not 24
            Assert.True(
               Math.Abs(DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds() - double.Parse(ret.N))
               < 100);
        }

        [Fact]
        public void CalculateTTLDeadline_RelativeOptionOnly_returnNowPlusRelative()
        {
            var relative = new TimeSpan(12, 0, 0);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = relative
            };
            var ret = DynamoDBCacheProviderHelper.CalculateTTLDeadline(options);
            //assert the deadline is approx 12 hours from now.
            //Effectively the same logic as CalculateTTLDeadline_BothOptions_PreferRelativeOption
            Assert.True(
               Math.Abs(DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds() - double.Parse(ret.N))
               < 100);
        }

        [Fact]
        public void CalculateTTLDeadline_ObsoluteOptionOnly_OptionIsInPast_Exception()
        {
            var absolute = DateTimeOffset.UtcNow.AddHours(-24);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = absolute
            };
            Assert.Throws<ArgumentOutOfRangeException>(() => DynamoDBCacheProviderHelper.CalculateTTLDeadline(options));
        }

        [Fact]
        public void CalculateTTLDeadline_ObsoluteOptionOnly_OptionIsInFuture_ReturnAbsoluteDeadline()
        {
            var absolute = DateTimeOffset.UtcNow.AddHours(24);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = absolute
            };
            var ret = DynamoDBCacheProviderHelper.CalculateTTLDeadline(options);
            //assert the deadline is approx 24 hours from now.
            Assert.True(
               Math.Abs(DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds() - double.Parse(ret.N))
               < 100);
        }
    }
}
