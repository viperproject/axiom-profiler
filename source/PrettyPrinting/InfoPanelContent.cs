using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Z3AxiomProfiler.PrettyPrinting
{
    public class InfoPanelContent
    {
        public static readonly Font DefaultFont = new Font("Consolas", 9, FontStyle.Regular);
        public static readonly Font TitleFont = new Font("Consolas", 13, FontStyle.Underline | FontStyle.Bold);
        public static readonly Font SubtitleFont = new Font("Consolas", 11, FontStyle.Underline | FontStyle.Bold);
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
            if(finalized) { throw new InvalidOperationException("Already finalized!");}
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
            textBuilder.Insert(index, text);
            foreach (var format in formats)
            {
                if (format.startIdx >= index)
                {
                    format.startIdx += text.Length;
                }
            }
            return this;
        }

        public void switchFormat(Font font, Color color)
        {
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

                // set format
                textBox.SelectionStart = textBox.TextLength;
                textBox.SelectionLength = 0;
                textBox.SelectionFont = currentFormat.font;
                textBox.SelectionColor = currentFormat.textColor;

                // actual writing
                textBox.AppendText(finalText.Substring(currentIndex, blockLength));

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