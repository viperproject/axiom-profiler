﻿//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using AxiomProfiler.QuantifierModel;
using AxiomProfiler.PrettyPrinting;

namespace AxiomProfiler
{
    public partial class ColorVisalizationForm : Form
    {

        //Define the colors
        private readonly List<Color> colors = new List<Color> {
                Color.Black, Color.Purple, Color.Blue,
                Color.Green, Color.Red, Color.Orange, Color.Cyan, Color.DarkGray, Color.Yellow,
                Color.YellowGreen, Color.Silver, Color.Salmon, Color.LemonChiffon, Color.Fuchsia,
                Color.ForestGreen, Color.Beige
                };

        private int ImageWidth;
        private int ImageHeight;

        public ColorVisalizationForm()
          : this(false, null)
        {
        }

        public ColorVisalizationForm(bool launchedFromAddin, Control ctrl)
        {
            InitializeComponent();
        }

        private void DisplayResults()
        {
	    if(quantifiers == null) return;
            ImageWidth = this.panel1.Width;
            ImageHeight = quantifiers.Count / ImageWidth + 1;
            if (ImageHeight < this.panel1.Height)
            {
                // This image fits into the display area. Adjust the size to fit the widget.
                ImageHeight = this.panel1.Height;
            }
            else
            {
                // This image is too large for the display area.
                ImageWidth = this.panel1.Width - 20;
                ImageHeight = quantifiers.Count / ImageWidth + 1;
            }

            Bitmap bmp = new Bitmap(ImageWidth, ImageHeight);

            for (int i = 0; i < ImageWidth; i++)
            {
                for (int j = 0; j < ImageHeight; j++)
                {
                    int index = j * ImageWidth + i;
                    if (quantifiers.Count > index)
                    {
                        int colorIndex = quantifierColorSorting.IndexOf(quantifiers[index]);
                        if ((colorIndex >= 0) && (colorIndex < colors.Count))
                        {
                            Color myColor = colors[colorIndex];
                            bmp.SetPixel(i, j, myColor);
                        }
                        else
                        {
                            bmp.SetPixel(i, j, Color.LightGray);
                        }
                    }
                    else
                    {
                        bmp.SetPixel(i, j, Color.WhiteSmoke);
                    }
                }
            }
            pictureBox1.Height = ImageHeight;
            pictureBox1.Width = ImageWidth;
            pictureBox1.Image = bmp;
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void saveBitmapAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.Filter = "Png file |*.png";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                pictureBox1.Image.Save(fileDialog.FileName, ImageFormat.Png);
            }
        }

        private void legendToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (quantifiers == null || quantifierColorSorting == null)
            {
                return;
            }
            String legend = "";
            int index = 0;
            foreach (Quantifier q in quantifierColorSorting)
            {
                if (colors.Count > index)
                {
                    legend += $"{colors[index]}: {q.PrintName}, Cost: {q.Cost}\n";
                }
                else
                {
                    break;
                }
                index++;
            }
            MessageBox.Show(legend);
        }


        List<Quantifier> quantifiers;
        List<Quantifier> quantifierColorSorting;

        public void setQuantifiers(List<Quantifier> quantifiers, List<Quantifier> quantifierColorSorting)
        {
            this.quantifiers = quantifiers;
            this.quantifierColorSorting = quantifierColorSorting;
            DisplayResults();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Point click = ((MouseEventArgs)e).Location;
            if ((click.Y >= 0) && (click.Y < ImageHeight) &&
                (click.X >= 0) && (click.X < ImageWidth))
            {
                int index = click.Y * ImageWidth + click.X;

                if (index < quantifiers.Count)
                {
                    Quantifier q = quantifiers[index];
                    int colorIndex = quantifierColorSorting.IndexOf(q);
                    if ((colorIndex >= 0) && (colorIndex < colors.Count))
                    {
                        this.colorBox.BackColor = colors[colorIndex];
                        var content = new InfoPanelContent();
                        q.InfoPanelText(content, new PrettyPrintFormat { printRuleDict = new PrintRuleDictionary() });
                        content.finalize();
                        this.boogieQuantifierText.Text = content.ToString();
                            
                        this.quantifierLinkedText.Text = q.ToString();
                    }
                    else
                    {
                        this.colorBox.BackColor = Color.White;
                        this.boogieQuantifierText.Text = "";
                        this.quantifierLinkedText.Text = "";
                    }
                }
            }
        }

        private void ColorVisalizationForm_SizeChanged(object sender, EventArgs e)
        {
            DisplayResults();
        }

        private void ColorVisalizationForm_ResizeBegin(object sender, EventArgs e)
        {
            panel1.AutoScroll = false;
        }

        private void ColorVisalizationForm_ResizeEnd(object sender, EventArgs e)
        {
            panel1.AutoScroll = true;
        }

    }
}
