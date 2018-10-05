using AxiomProfiler.QuantifierModel;
using System;
using System.Linq;

namespace AxiomProfiler.PrettyPrinting
{

    class EqualityExplanationPrinter : EqualityExplanationVisitor<object, Tuple<InfoPanelContent, PrettyPrintFormat, bool, int>>
    {
        public static readonly EqualityExplanationPrinter singleton = new EqualityExplanationPrinter();

        private static string getIndentString(PrettyPrintFormat format, int descriptionIndent)
        {
            if (descriptionIndent < 2) return "";
            var descriptionIndentString = PrintConstants.indentDiff + String.Join("", Enumerable.Repeat(" ", descriptionIndent - 2));
            var explanationIndentString = String.Join("", Enumerable.Repeat(PrintConstants.indentDiff, format.CurrentEqualityExplanationPrintingDepth));
            return descriptionIndentString + explanationIndentString;
        }

        public override object Direct(DirectEqualityExplanation target, Tuple<InfoPanelContent, PrettyPrintFormat, bool, int> arg)
        {
            var content = arg.Item1;
            var format = arg.Item2;
            var shouldPrintSource = arg.Item3;
            var indent = arg.Item4;

            var indentString = getIndentString(format, indent);

            if (shouldPrintSource)
            {
                content.Append(indentString);
                target.source.PrettyPrint(content, format, indent);
            }
            content.switchToDefaultFormat();
            content.Append($"\n{indentString}");
            target.equality.printName(content, format);
            if (target.equality.Responsible != null)
            {
                content.Append($" ({target.equality.Responsible.Quant.PrintName})");
            }
            else
            {
                content.Append($" (asserted)");
            }
            content.Append($"\n{indentString}");
            target.target.PrettyPrint(content, format, indent);

            return null;
        }

        public override object Transitive(TransitiveEqualityExplanation target, Tuple<InfoPanelContent, PrettyPrintFormat, bool, int> arg)
        {
            var content = arg.Item1;
            var format = arg.Item2;
            var shouldPrintSource = arg.Item3;
            var indent = arg.Item4;

            var indentString = getIndentString(format, indent);

            for (var i = 0; i < target.equalities.Length; ++i)
            {
                visit(target.equalities[i], Tuple.Create(content, format, i == 0 && shouldPrintSource, indent));
            }

            if (target.equalities.Length == 0)
            {
                if (shouldPrintSource)
                {
                    content.Append(indentString);
                    target.source.PrettyPrint(content, format, indent);
                }
                content.switchToDefaultFormat();
                content.Append($"\n{indentString}=\n{indentString}");
                target.target.PrettyPrint(content, format, indent);
            }

            return null;
        }

        public override object Congruence(CongruenceExplanation target, Tuple<InfoPanelContent, PrettyPrintFormat, bool, int> arg)
        {
            var content = arg.Item1;
            var format = arg.Item2;
            var shouldPrintSource = arg.Item3;
            var indent = arg.Item4;

            var indentString = getIndentString(format, indent);

            if (shouldPrintSource)
            {
                content.Append(indentString);
                target.source.PrettyPrint(content, format, indent);
            }

            content.switchToDefaultFormat();

            content.Append($"\n{indentString}= (congruence)\n");

            if (format.CurrentEqualityExplanationPrintingDepth < format.MaxEqualityExplanationPrintingDepth)
            {
                content.Append($"{indentString}\n{indentString}{PrintConstants.indentDiff}Argument Equalities:\n");
                var recursiveArg = Tuple.Create(content, format.NextEqualityExplanationPrintingDepth(), true, indent);

                visit(target.sourceArgumentEqualities.First(), recursiveArg);
                foreach (var eqality in target.sourceArgumentEqualities.Skip(1))
                {
                    content.switchToDefaultFormat();
                    content.Append($"\n{indentString}{PrintConstants.indentDiff}\n");
                    visit(eqality, recursiveArg);
                }
                content.switchToDefaultFormat();
                content.Append($"\n{indentString}\n");
            }

            content.Append(indentString);
            target.target.PrettyPrint(content, format, indent);

            return null;
        }

        public override object Theory(TheoryEqualityExplanation target, Tuple<InfoPanelContent, PrettyPrintFormat, bool, int> arg)
        {
            var content = arg.Item1;
            var format = arg.Item2;
            var shouldPrintSource = arg.Item3;
            var indent = arg.Item4;

            var indentString = getIndentString(format, indent);

            if (shouldPrintSource)
            {
                content.Append(indentString);
                target.source.PrettyPrint(content, format, indent);
            }
            content.switchToDefaultFormat();
            content.Append($"\n{indentString}= ({target.TheoryName} theory)\n{indentString}");
            target.target.PrettyPrint(content, format, indent);

            return null;
        }

        public override object RecursiveReference(RecursiveReferenceEqualityExplanation target, Tuple<InfoPanelContent, PrettyPrintFormat, bool, int> arg)
        {
            var content = arg.Item1;
            var format = arg.Item2;
            var shouldPrintSource = arg.Item3;
            var indent = arg.Item4;

            var indentString = getIndentString(format, indent);

            if (shouldPrintSource)
            {
                content.Append(indentString);
                target.source.PrettyPrint(content, format, indent);
            }
            content.switchToDefaultFormat();
            content.Append($"\n{indentString}= ({format.equalityNumbers[target.ReferencedExplanation]}{(target.isPrime ? "'" : "")}){(target.GenerationOffset > 0 ? "-" + target.GenerationOffset : "")}");
            content.Append($"\n{indentString}");
            target.target.PrettyPrint(content, format, indent);

            return null;
        }
    }
}
