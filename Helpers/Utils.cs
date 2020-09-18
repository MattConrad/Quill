using System;
using System.Collections.Generic;

namespace Quill.Helpers
{
    public static class Utils
    {
        //modified from https://www.stum.de/2008/10/20/base36-encoderdecoder-in-c/
        // all we're using this for is to get nearly-unique values from DateTime.Ticks.
        public static string EncodeTicks(long input)
        {
            if (input < 0) throw new ArgumentException("encode cannot be negative");

            char[] clistarr = "0123456789bcdfghjklmnpqrstvwxyz".ToCharArray();
            var result = new Stack<char>();
            while (input != 0)
            {
                    result.Push(clistarr[input % 31]);
                    input /= 31;
            }
            return new string(result.ToArray());
        }
    }
}
