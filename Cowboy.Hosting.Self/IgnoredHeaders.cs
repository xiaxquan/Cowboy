﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Hosting.Self
{
    public static class IgnoredHeaders
    {
        private static readonly HashSet<string> knownHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "content-length",
            "content-type",
            "transfer-encoding",
            "keep-alive"
        };

        public static bool IsIgnored(string headerName)
        {
            return knownHeaders.Contains(headerName);
        }
    }
}
