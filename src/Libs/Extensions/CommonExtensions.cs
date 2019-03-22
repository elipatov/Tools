using System;
using System.Text;

namespace Libs
{
    public static class CommonExtensions
    {
        public static string ToBitsString(byte[] bytes)
        {
            if (bytes.Length == 0) return String.Empty;
            var builder = new StringBuilder();

            for (int i = bytes.Length - 1; i >= 0; --i)
            {
                byte b = bytes[i];
                for (int bit = 7; bit >= 0; --bit)
                {
                    bool isSet = (b & (1 << bit)) != 0;
                    builder.Append(isSet ? "1" : "0");
                }
                builder.Append("_");
            }

            builder.Remove(builder.Length - 1, 1);
            return builder.ToString();
        }
    }
}
