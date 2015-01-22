using System.Xml;

namespace Microsoft.Web.XmlTransform.Extended
{
    public interface IXmlOriginalDocumentService
    {
        XmlNodeList SelectNodes(string path, XmlNamespaceManager nsmgr);
    }
}
