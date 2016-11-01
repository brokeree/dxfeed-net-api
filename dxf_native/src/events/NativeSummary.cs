﻿/// Copyright (C) 2010-2016 Devexperts LLC
///
/// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
/// If a copy of the MPL was not distributed with this file, You can obtain one at
/// http://mozilla.org/MPL/2.0/.

using System.Globalization;
using com.dxfeed.api.events;
using com.dxfeed.native.api;

namespace com.dxfeed.native.events
{
    public class NativeSummary : MarketEvent, IDxSummary
    {
        private readonly DxSummary summary;

        internal unsafe NativeSummary(DxSummary* summary, string symbol) : base(symbol)
        {
            this.summary = *summary;
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Summary: {{{10}, DayId: {0}, DayOpenPrice: {1}, DayHighPrice: {2}, DayLowPrice: {3}, " +
                "DayClosePrice: {4}, PrevDayId: {5}, PrevDayClosePrice: {6}, OpenInterest: {7}, " +
                "Flags: {8}, ExchangeCode: {9} }}",
                DayId, DayOpenPrice, DayHighPrice, DayLowPrice,
                DayClosePrice, PrevDayId, PrevDayClosePrice, OpenInterest,
                Flags, ExchangeCode, EventSymbol);
        }

        #region Implementation of IDxSummary

        public int DayId
        {
            get { return summary.day_id; }
        }

        public double DayOpenPrice
        {
            get { return summary.day_open_price; }
        }

        public double DayHighPrice
        {
            get { return summary.day_high_price; }
        }

        public double DayLowPrice
        {
            get { return summary.day_low_price; }
        }

        public double DayClosePrice
        {
            get { return summary.day_close_price; }
        }

        public int PrevDayId
        {
            get { return summary.prev_day_id; }
        }

        public double PrevDayClosePrice
        {
            get { return summary.prev_day_close_price; }
        }

        public long OpenInterest
        {
            get { return summary.open_interest; }
        }

        public long Flags
        {
            get { return summary.flags; }
        }

        public char ExchangeCode
        {
            get { return summary.exchange_code; }
        }

        #endregion
    }
}