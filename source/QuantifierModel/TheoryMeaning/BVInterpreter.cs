using System;
using System.Numerics;
using System.Text;

namespace AxiomProfiler.QuantifierModel.TheoryMeaning
{
    class BVInterpreter : TheoryInterpreter
    {
        private static readonly char[] splitter = new char[] { ' ' };

        public override string GetPrettyString(string meaning)
        {
            if (meaning.StartsWith("(") && meaning.EndsWith(")"))
            {
                var words = meaning.Substring(1, meaning.Length - 2).Split(splitter);
                if (words.Length == 2)
                {
                    var data = words[1];
                    if (data.StartsWith("#d"))
                    {
                        if (!BigInteger.TryParse(data.Substring(2), out var parsed)) return meaning;
                        var hexString = parsed.ToString("x");
                        if (hexString.StartsWith("0")) hexString = hexString.Substring(1);
                        data = "#x" + hexString;
                    }
                    if (data.StartsWith("#x"))
                    {
                        var stringBuilder = new StringBuilder($"bv (length: {words[0]}): #x");
                        
                        for (var i = 2; i < data.Length; i += 4)
                        {
                            if (i != 2) stringBuilder.Append(" ");
                            stringBuilder.Append(data, i, Math.Min(4, data.Length - i));
                        }

                        return stringBuilder.ToString();
                    }
                }
            }
            return meaning;
        }
    }
}
