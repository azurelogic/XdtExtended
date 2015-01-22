using System.Xml;

namespace Microsoft.Web.XmlTransform
{
    public class XmlTransformableDocument : XmlFileInfoDocument, IXmlOriginalDocumentService
    {
        #region private data members
        private XmlDocument _xmlOriginal;
        #endregion

        #region public interface

        public bool IsChanged
        {
            get
            {
                if (_xmlOriginal == null)
                {
                    // No transformation has occurred
                    return false;
                }

                return !IsXmlEqual(_xmlOriginal, this);
            }
        }
        #endregion

        #region Change support
        internal void OnBeforeChange()
        {
            if (_xmlOriginal == null)
            {
                CloneOriginalDocument();
            }
        }

        internal void OnAfterChange()
        {
        }
        #endregion

        #region Helper methods
        private void CloneOriginalDocument()
        {
            _xmlOriginal = (XmlDocument)Clone();
        }

        private bool IsXmlEqual(XmlDocument xmlOriginal, XmlDocument xmlTransformed)
        {
            // FUTURE: Write a comparison algorithm to see if xmlLeft and
            // xmlRight are different in any significant way. Until then,
            // assume there's a difference.
            return false;
        }
        #endregion

        #region IXmlOriginalDocumentService Members
        XmlNodeList IXmlOriginalDocumentService.SelectNodes(string xpath, XmlNamespaceManager nsmgr)
        {
            return _xmlOriginal != null ? _xmlOriginal.SelectNodes(xpath, nsmgr) : null;
        }

        #endregion
    }
}
