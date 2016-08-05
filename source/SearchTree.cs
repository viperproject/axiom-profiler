﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using AxiomProfiler.QuantifierModel;

namespace AxiomProfiler
{
    public partial class SearchTree : Form
    {
        public SearchTree(Model model, AxiomProfiler z3AxiomProfiler)
        {
            this.model = model;
            InitializeComponent();
            this.pictureBox1.Paint += this.PaintTree;
            this.MouseWheel += this.pictureBox1_MouseWheel;
            this.z3AxiomProfiler = z3AxiomProfiler;
        }

        Graphics gfx;

        float scale = -1;
        float offX = 0, offY = 0;
        Scope selectedScope;

        float lastMouseX, lastMouseY;
        float closestsDistance;
        Scope closestsScope;
        bool needSelect;
        PointF middle;
        float radius;

        private PointF ToScreen(PointF p)
        {
            return new PointF(p.X * scale + offX, p.Y * scale + offY);
        }

        private float Distance2(float dx, float dy) { return dx * dx + dy * dy; }

        private PointF LineAtAng(Pen pen, PointF p, float len, float ang)
        {
            PointF t = p;
            t.X += (float)(Math.Sin(ang) * len);
            t.Y -= (float)(Math.Cos(ang) * len);
            gfx.DrawLine(pen, ToScreen(p), ToScreen(t));
            return t;
        }

        private void PaintSubtree(PointF ourPos, float leftAng, float rightAng, Scope s, bool selected)
        {
            var pp = ToScreen(ourPos);

            var dist = Distance2(pp.X - lastMouseX, pp.Y - lastMouseY);
            if (dist < closestsDistance)
            {
                closestsDistance = dist;
                closestsScope = s;
            }

            var br = Brushes.Blue;
            if (s == selectedScope)
            {
                selected = true;
                br = Brushes.Orange;
            }

            float sz = 4;
            gfx.FillEllipse(br, pp.X - sz, pp.Y - sz, sz * 2, sz * 2);

            int n = s.ChildrenScopes.Count;
            float radiusLeft = radius - (float)Math.Sqrt(Distance2(middle.X - ourPos.X, middle.X - ourPos.X));

            if (n > 0)
            {
                float angStep = (rightAng - leftAng) / (s.InstanceCount - s.OwnInstanceCount);
                float curAng = leftAng;

                foreach (var c in s.ChildrenScopes)
                {
                    if (c.InstanceCount == 0) continue;
                    float nextAng = curAng + angStep * c.InstanceCount;
                    float midAng = (curAng + nextAng) / 2;

                    var len = c.OwnInstanceCount;
                    PointF t = LineAtAng(selected ? Pens.Red : c == selectedScope ? Pens.Orange : Pens.Black, ourPos, len, midAng);

                    float alpha = (nextAng - curAng) / 2;
                    float beta = alpha;

                    if (alpha < 0.4 * Math.PI)
                    {
                        float nextLeft = radiusLeft - len;
                        if (nextLeft < radiusLeft / 4)
                            nextLeft = radiusLeft / 4;
                        beta = (float)Math.Atan(radiusLeft * Math.Tan(alpha) / nextLeft);
                    }

                    PaintSubtree(t, midAng - beta, midAng + beta, c, selected);

                    if (alpha > Math.PI * 0.05)
                    {
                        var len2 = 0.9f * len;
                        LineAtAng(Pens.Lime, ourPos, len2, midAng - alpha);
                        LineAtAng(Pens.Lime, ourPos, len2, midAng + alpha);
                    }

                    curAng = nextAng;
                }
            }
        }

        private void PaintTree(object sender, PaintEventArgs e)
        {
            var root = model.rootScope;

            closestsScope = null;
            closestsDistance = 50;

            gfx = e.Graphics;
            radius = root.RecurisveInstanceDepth - root.OwnInstanceCount;
            var r = gfx.ClipBounds;
            gfx.FillRectangle(Brushes.White, r);

            middle = new PointF(0, 0);

            if (scale < 0)
            {
                scale = pictureBox1.Height / radius;
                offX = r.X + r.Width / 2;
                offY = r.Bottom - 10;
                SetTitle();
            }

            PaintSubtree(middle, (float)(-0.8 * Math.PI), (float)(+0.8 * Math.PI), root, false);
            if (needSelect)
            {
                selectedScope = closestsScope;
                if (selectedScope != null)
                    z3AxiomProfiler.ExpandScope(selectedScope);
                needSelect = false;
                pictureBox1.Invalidate();
            }
        }

        private void pictureBox1_Resize(object sender, EventArgs e)
        {
            pictureBox1.Invalidate();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }

        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                needSelect = true;
                pictureBox1.Invalidate();
            }
        }

        private void pictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta != 0)
            {
                var f = 1.2f;
                if (e.Delta < 0)
                    f = 1 / f;

                scale *= f;
                SetTitle();

                var x = e.X - pictureBox1.Left;
                var y = e.Y - pictureBox1.Top;

                offX = (1 - f) * x + offX * f;
                offY = (1 - f) * y + offY * f;
                pictureBox1.Invalidate();
            }
        }

        private void SetTitle()
        {
            var sc = scale > 0.1 ? $"{scale:0.000}" : $"1 / {(int)(1 / scale)}";
            this.Text = $"{model.LogFileName} [zoom: {sc}]";
        }


        int prevX = -1, prevY;
        private Model model;
        private AxiomProfiler z3AxiomProfiler;
        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            lastMouseX = e.X;
            lastMouseY = e.Y;

            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (prevX != -1)
                {
                    offX += e.X - prevX;
                    offY += e.Y - prevY;
                    pictureBox1.Invalidate();
                }
                prevX = e.X;
                prevY = e.Y;
            }
            else
            {
                prevX = -1;
            }
        }

        private void SearchTree_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        public void SelectScope(Scope scope)
        {
            selectedScope = scope;
            pictureBox1.Invalidate();
        }
    }
}
