using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Z3AxiomProfiler.QuantifierModel;

namespace Z3AxiomProfiler
{

    public partial class InstantiationFilter : Form
    {
        // original, unfiltered list of instantiations
        private readonly List<Instantiation> original;
        private Func<Instantiation, double> ordFunc = inst => inst.LineNo;
        public List<Instantiation> filtered; 
        public InstantiationFilter(List<Instantiation> instantiationsToFilter)
        {
            InitializeComponent();
            original = instantiationsToFilter;
        }

        protected override void OnLoad(EventArgs e)
        {
            var quantifiers = original.Select(inst => inst.Quant).Distinct().ToList();
            foreach (var quant in quantifiers)
            {
                quantSelectionBox.Items.Add(quant, true);
            }
            doFilter();
        }

        private void doFilter()
        {
            filtered = original
                .Where(inst => inst.Depth <= maxDepthUpDown.Value)
                .Where(inst => quantSelectionBox.CheckedItems.Contains(inst.Quant))
                .OrderBy(ordFunc)
                .Take((int) maxNewNodesUpDown.Value)
                .ToList();
            numberNodesMatchingLabel.Text = filtered.Count.ToString();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void quantSelectionBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            BeginInvoke((Action)doFilter);
        }

        private void updateFilter(object sender, EventArgs e)
        {
            doFilter();
        }

        private void InstantiationFilter_Resize(object sender, EventArgs e)
        {
            const int diff = 2;
            var middle = Width/2;
            okButton.Left = middle - okButton.Width - diff;
            cancelButton.Left = middle + diff;
        }

        private void sortSelectionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch ((string)sortSelectionBox.SelectedItem)
            {
                case "Line No":
                    ordFunc = inst => inst.LineNo;
                    break;
                case "Line No (desc)":
                    ordFunc = inst => -inst.LineNo;
                    break;
                case "Depth":
                    ordFunc = inst => inst.Depth;
                    break;
                case "Depth (desc)":
                    ordFunc = inst => -inst.Depth;
                    break;
                case "Cost":
                    ordFunc = inst => inst.Cost;
                    break;
                case "Cost (desc)":
                    ordFunc = inst => -inst.Cost;
                    break;
                default:
                    ordFunc = inst => inst.LineNo;
                    break;
            }
            doFilter();
        }
    }
}
