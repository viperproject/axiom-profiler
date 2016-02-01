using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

namespace Z3AxiomProfiler.PrettyPrinting
{
    public class InfoPanelContent
    {
        public static readonly Font DefaultFont = new Font("Consolas", 9, FontStyle.Regular);
        public static readonly Font TitleFont = new Font("Consolas", 9, FontStyle.Underline | FontStyle.Bold);
        public static readonly Font SubtitleFont = new Font("Consolas", 9, FontStyle.Underline);
        public static readonly Font BoldFont = new Font(DefaultFont, FontStyle.Bold);
        public static readonly Font ItalicFont = new Font(DefaultFont, FontStyle.Italic);

        private readonly StringBuilder textBuilder = new StringBuilder();
        private bool finalized;
        private readonly List<TextFormat> formats = new List<TextFormat>();

        private TextFormat currentFormat = TextFormat.defaultFormat(0);
        private TextFormat nextFormat;
        private string finalText;
        private int currentIndex;
        private int formatSwitchIndex;
        private IEnumerator<TextFormat> formatEnumerator;
        public bool finished { get; private set; }

        public int Length => textBuilder.Length;

        public override string ToString()
        {
            return !finalized ? "not finalized yet!" : string.Copy(finalText);
        }

        public void finalize()
        {
            if (finalized) { throw new InvalidOperationException("Already finalized!"); }
            if (currentFormat.startIdx <= textBuilder.Length)
            {
                formats.Add(currentFormat);
            }

            finalText = textBuilder.ToString();
            finalized = true;
            reset();
        }

        private void advanceFormat()
        {
            currentFormat = formatEnumerator.Current;
            nextFormat = formatEnumerator.MoveNext() ? formatEnumerator.Current : null;
            formatSwitchIndex = nextFormat?.startIdx ?? finalText.Length;
        }

        public InfoPanelContent Append(string text)
        {
            checkFinalized();
            textBuilder.Append(text);
            return this;
        }

        public InfoPanelContent Append(char text)
        {
            checkFinalized();
            textBuilder.Append(text);
            return this;
        }

        public InfoPanelContent Insert(int index, string text)
        {
            if (string.IsNullOrEmpty(text)) return this;
            textBuilder.Insert(index, text);

            foreach (var format in formats.Where(format => format.startIdx >= index))
            {
                format.startIdx += text.Length;
            }

            if (currentFormat.startIdx >= index)
            {
                currentFormat.startIdx += text.Length;
            }
            return this;
        }

        public InfoPanelContent Insert(int index, string text, Font font, Color color)
        {
            if (string.IsNullOrEmpty(text)) return this;
            Insert(index, text);
            var insertFormat = new TextFormat(index, font, color);

            if (currentFormat.startIdx < index)
            {
                // case0a: format is the same as current -> no change necessary
                if (textformatEqual(currentFormat, insertFormat)) return this;

                // case0b: insert into current format space
                formats.Add(currentFormat);
                formats.Add(insertFormat);
                currentFormat = new TextFormat(index + text.Length, currentFormat.font, currentFormat.textColor);
                return this;
            }

            // case1: insert is before current format

            // find out whether inserting the format is necessary
            TextFormat prevFormat = null;
            TextFormat afterFormat = null;
            var insertIdx = -1;
            foreach (var format in formats)
            {
                insertIdx++;
                prevFormat = afterFormat;
                afterFormat = format;
                if (afterFormat.startIdx > index) break;
            }

            if (prevFormat == null)
            {
                // all the same, nothing to do.
                if (textformatEqual(insertFormat, afterFormat)) return this;
                //case1a: only one format has been stored until now.
                if (insertFormat.startIdx < afterFormat.startIdx)
                {
                    // insert before and be finished
                    formats.Insert(0, insertFormat);
                }
                else
                {
                    // insert after and duplicate afterFormat
                    formats.Add(insertFormat);
                    formats.Add(new TextFormat(index + text.Length, afterFormat.font, afterFormat.textColor));
                }
                return this;
            }

            // all the same, nothing to do.
            if (textformatEqual(insertFormat, prevFormat)) return this;

            // insert after prevFormat and duplicate prevFormat in reverse order (at the same idx)
            if (index + text.Length != afterFormat.startIdx)
            {
                formats.Insert(insertIdx, new TextFormat(index + text.Length, prevFormat.font, prevFormat.textColor));
            }
            formats.Insert(insertIdx, insertFormat);
            return this;
        }

        private bool textformatEqual(TextFormat format1, TextFormat format2)
        {
            return format1.font.Equals(format2.font)
                   && format1.textColor.ToArgb() == format2.textColor.ToArgb();
        }

        public void switchFormat(Font font, Color color)
        {
            // switch to the same format -> nothing to do
            if (currentFormat.font.Equals(font) && currentFormat.textColor.ToArgb() == color.ToArgb()) return;

            if (currentFormat.startIdx < textBuilder.Length)
            {
                formats.Add(currentFormat);
            }
            currentFormat = new TextFormat(textBuilder.Length, font, color);
        }

        public void switchToDefaultFormat()
        {
            switchFormat(DefaultFont, Color.Black);
        }

        private void checkFinalized()
        {
            if (finalized) throw new InvalidOperationException("Info panel content is already finalized!");
        }

        public void writeToTextBox(RichTextBox textBox, int batchsize)
        {
            do
            {
                // calculate block of things that are printed the same.
                var blockLength = Math.Min(formatSwitchIndex - currentIndex, batchsize);
                batchsize -= blockLength;
                var startIdx = textBox.TextLength;

                // actual writing
                textBox.AppendText(finalText.Substring(currentIndex, blockLength));

                // set format
                textBox.SelectionStart = startIdx;
                textBox.SelectionLength = textBox.TextLength;
                textBox.SelectionFont = currentFormat.font;
                textBox.SelectionColor = currentFormat.textColor;

                // bookkeeping
                currentIndex += blockLength;

                if (currentIndex == formatSwitchIndex)
                {
                    advanceFormat();
                }
            } while (currentIndex < finalText.Length && batchsize > 0);

            if (currentIndex == finalText.Length)
            {
                finished = true;
            }
        }

        public void reset()
        {
            currentIndex = 0;
            formatEnumerator = formats.GetEnumerator();
            formatEnumerator.MoveNext();
            advanceFormat();
        }

        public bool inProgress()
        {
            return currentIndex > 0 && !finished;
        }
    }

    internal class TextFormat
    {
        // needs to be mutable for insert functionality
        public int startIdx;
        public readonly Font font;
        public readonly Color textColor;

        public TextFormat(int startIndex, Font font, Color color)
        {
            startIdx = startIndex;
            textColor = color;
            this.font = font;
        }

        public static TextFormat defaultFormat(int startIndex)
        {
            return new TextFormat(startIndex, InfoPanelContent.DefaultFont, Color.Black);
        }
    }
}