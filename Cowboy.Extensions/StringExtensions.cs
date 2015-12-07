﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy
{
    public static class StringExtensions
    {
        public static DynamicDictionary AsQueryDictionary(this string queryString)
        {
            var coll = HttpUtility.ParseQueryString(queryString);
            var ret = new DynamicDictionary();
            var requestQueryFormMultipartLimit = 1000;

            var found = 0;
            foreach (var key in coll.AllKeys.Where(key => key != null))
            {
                ret[key] = coll[key];

                found++;

                if (found >= requestQueryFormMultipartLimit)
                {
                    break;
                }
            }

            return ret;
        }
    }
}
