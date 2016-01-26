namespace Z3AxiomProfiler.PrettyPrinting
{
    public interface IPrintable
    {
        string SummaryInfo();

        string InfoPanelText(PrettyPrintFormat format);
    }
}