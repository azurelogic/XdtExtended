using System;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Xml;

namespace Microsoft.Web.XmlTransform.Extended
{
    [Serializable]
    public sealed class XmlNodeException : XmlTransformationException
    {
        private readonly XmlFileInfoDocument _document;
        private readonly IXmlLineInfo _lineInfo;

        public static Exception Wrap(Exception ex, XmlNode node)
        {
            return ex is XmlNodeException ? ex : new XmlNodeException(ex, node);
        }

        public XmlNodeException(Exception innerException, XmlNode node)
            : base(innerException.Message, innerException)
        {
            _lineInfo = node as IXmlLineInfo;
            _document = node.OwnerDocument as XmlFileInfoDocument;
        }

        public XmlNodeException(string message, XmlNode node)
            : base(message)
        {
            _lineInfo = node as IXmlLineInfo;
            _document = node.OwnerDocument as XmlFileInfoDocument;
        }

        public bool HasErrorInfo
        {
            get
            {
                return _lineInfo != null;
            }
        }

        public string FileName
        {
            get
            {
                return _document != null ? _document.FileName : null;
            }
        }

        public int LineNumber
        {
            get
            {
                return _lineInfo != null ? _lineInfo.LineNumber : 0;
            }
        }

        public int LinePosition
        {
            get
            {
                return _lineInfo != null ? _lineInfo.LinePosition : 0;
            }
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("document", _document);
            info.AddValue("lineInfo", _lineInfo);
        }
    }
}
