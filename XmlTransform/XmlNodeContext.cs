using System.Xml;

namespace Microsoft.Web.XmlTransform.Extended
{
    internal class XmlNodeContext
    {
        #region private data members
        private readonly XmlNode _node;
        #endregion

        public XmlNodeContext(XmlNode node)
        {
            _node = node;
        }

        #region data accessors
        public XmlNode Node
        {
            get
            {
                return _node;
            }
        }

        public bool HasLineInfo
        {
            get
            {
                return _node is IXmlLineInfo;
            }
        }

        public int LineNumber
        {
            get
            {
                var lineInfo = _node as IXmlLineInfo;
                return lineInfo != null ? lineInfo.LineNumber : 0;
            }
        }

        public int LinePosition
        {
            get
            {
                var lineInfo = _node as IXmlLineInfo;
                return lineInfo != null ? lineInfo.LinePosition : 0;
            }
        }
        #endregion
    }
}
