using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Z3AxiomProfiler.PrettyPrinting
{
    public class InfoPanelContent
    {
        public static readonly Font DefaultFont = new Font("Consolas", 9, FontStyle.Regular);
        public static readonly Font TitleFont = new Font("Consolas", 15, FontStyle.Underline);
        public static readonly Font SubtitleFont = new Font("Consolas", 12, FontStyle.Regular);
        public static readonly Font BoldFont = new Font(DefaultFont, FontStyle.Bold);
        public static readonly Font ItalicFont = new Font(DefaultFont, FontStyle.Italic);

        private readonly StringBuilder textBuilder = new StringBuilder();
        private bool finalized;
        private readonly List<TextFormat> formats = new List<TextFormat>();

        private TextFormat currentFormat = TextFormat.defaultFormat(0);
        private string finalText;


        public void finalize()
        {
            finalText = textBuilder.ToString();
            finalized = true;
        }

        public void appendText(string text)
        {
            checkFinalized();
            textBuilder.Append(text);
        }

        public void switchFormat(Font font, Color color)
        {
            if (currentFormat.startIdx < textBuilder.Length)
            {
                formats.Add(currentFormat);
            }
            currentFormat = new TextFormat(textBuilder.Length, font, color);
        }

        private void checkFinalized()
        {
            if (finalized) throw new InvalidOperationException("Info panel content is already finalized!");
        }
    }

    class TextFormat
    {
        public readonly int startIdx;
        public readonly Font highlightFont;
        public readonly Color highlightColor;

        public TextFormat(int startIndex, Font font, Color color)
        {
            startIdx = startIndex;
            highlightColor = color;
            highlightFont = font;
        }

        public static TextFormat defaultFormat(int startIndex)
        {
            return new TextFormat(startIndex, InfoPanelContent.DefaultFont, Color.Black);
        }
    }
}