using System.Collections.Generic;

namespace AxiomProfiler.QuantifierModel.TheoryMeaning
{
    class TheoryMeaningInterpretation
    {
        public static TheoryMeaningInterpretation singleton = new TheoryMeaningInterpretation();

        private readonly Dictionary<string, TheoryInterpreter> interpreters = new Dictionary<string, TheoryInterpreter>() {
            ["arith"] = new ArithInterpreter(),
            ["bv"] = new BVInterpreter()
        };

        private TheoryMeaningInterpretation() {}

        public string GetPrettyStringForTheoryMeaning(string theory, string meaning)
        {
            if (interpreters.TryGetValue(theory, out var interpreter))
            {
                return interpreter.GetPrettyString(meaning);
            }
            return meaning;
        }
    }

    abstract class TheoryInterpreter
    {
        public abstract string GetPrettyString(string meaning);
    }
}
