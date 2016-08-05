using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace AxiomProfiler
{
  public partial class SearchBox : Form
  {
    public SearchBox(AxiomProfiler a)
    {
      axprof = a;
      InitializeComponent();
      textBox1.Text = a.SearchText;
    }

    class NodeText
    {
      internal TreeNode n;
      public NodeText(TreeNode n) { this.n = n; }
      public override string ToString()
      {
        return n.Text;
      }
    }

    List<NodeText> nodes = new List<NodeText>();
    AxiomProfiler axprof;

    private void AddNodes(TreeNodeCollection coll)
    {
      foreach (TreeNode n in coll) {
        nodes.Add(new NodeText(n));
        if (n.IsExpanded) {
          AddNodes(n.Nodes);
        }
      }
    }

    public void SetFilter(string s)
    {
      axprof.SearchText = textBox1.Text;
      var words0 = s.Split(' ');
      var words = (from w in words0 where w != "" select w.ToLower()).ToList();
        listBox1.BeginUpdate();
      listBox1.Items.Clear();
      listBox1.Items.AddRange((from n in nodes let x = n.ToString().ToLower()
                               let wrong = words.Any(w => !x.Contains(w))
                               where !wrong select n).Cast<object>().ToArray());
      listBox1.EndUpdate();
    }

    public void Populate(TreeNodeCollection coll)
    {
      nodes.Clear();
      AddNodes(coll);
      SetFilter(textBox1.Text);
    }

    private void textBox1_TextChanged(object sender, EventArgs e)
    {
      SetFilter(textBox1.Text);
    }

    private void SearchBox_FormClosing(object sender, FormClosingEventArgs e)
    {
      
    }

    private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
    {
      if (e.KeyChar == (char)13 || e.KeyChar == (char)27)
        e.Handled = true;

    }

    private void textBox1_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Down) {
        listBox1.SelectedIndex = 0;
        listBox1.Focus();
        e.Handled = true;
      } else if (e.KeyCode == Keys.Enter) {
        Execute(true);
        e.Handled = true;
      } else if (e.KeyCode == Keys.Escape) {
        Hide();
        e.Handled = true;
      }
    }

    private void Execute(bool first)
    {
      if (listBox1.Items.Count == 0) return;

      NodeText n =
        (first ? listBox1.Items[0] : listBox1.SelectedItem) as NodeText;
      if (n != null) {
        axprof.Activate(n.n);
        this.Hide();
      }
    }

    private void listBox1_Click(object sender, EventArgs e)
    {
      Execute(false);
    }

    private void listBox1_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.KeyCode == Keys.Enter) {
        Execute(false);
        e.Handled = true;
      }
    }
  }
}
