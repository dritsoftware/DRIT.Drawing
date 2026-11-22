namespace DRIT.Drawing.Vector.Emf
{
    /// <summary>
    /// Selects the embedded graphics stream emitted in an EMF document.
    /// </summary>
    public enum EmfOutputMode
    {
        /// <summary>
        /// Emit classic EMF records only.
        /// </summary>
        Classic,

        /// <summary>
        /// Emit EMF+ records together with the classic EMF fallback.
        /// </summary>
        EmfPlusDual,
    }
}
