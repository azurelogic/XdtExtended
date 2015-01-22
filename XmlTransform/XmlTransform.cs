using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;

namespace Microsoft.Web.XmlTransform.Extended
{
    public enum MissingTargetMessage
    {
        None,
        Information,
        Warning,
        Error,
    }

    [Flags]
    public enum TransformFlags
    {
        None = 0,
        ApplyTransformToAllTargetNodes = 1,
        UseParentAsTargetNode = 2,
    }


    public abstract class Transform
    {
        #region private data members

        private XmlTransformationLogger _logger;
        private XmlElementContext _context;
        private XmlNode _currentTransformNode;
        private XmlNode _currentTargetNode;

        private string _argumentString;
        private IList<string> _arguments;
        #endregion

        protected Transform()
            : this(TransformFlags.None)
        {
        }

        protected Transform(TransformFlags flags)
            : this(flags, MissingTargetMessage.Warning)
        {
        }

        protected Transform(TransformFlags flags, MissingTargetMessage message)
        {
            MissingTargetMessage = message;
            ApplyTransformToAllTargetNodes = (flags & TransformFlags.ApplyTransformToAllTargetNodes) == TransformFlags.ApplyTransformToAllTargetNodes;
            UseParentAsTargetNode = (flags & TransformFlags.UseParentAsTargetNode) == TransformFlags.UseParentAsTargetNode;
        }

        protected bool ApplyTransformToAllTargetNodes { get; set; }

        protected bool UseParentAsTargetNode { get; set; }

        protected MissingTargetMessage MissingTargetMessage { get; set; }

        protected abstract void Apply();

        protected XmlNode TransformNode
        {
            get
            {
                return _currentTransformNode ?? _context.TransformNode;
            }
        }

        protected XmlNode TargetNode
        {
            get
            {
                if (_currentTargetNode == null)
                {
                    foreach (XmlNode targetNode in TargetNodes)
                    {
                        return targetNode;
                    }
                }
                return _currentTargetNode;
            }
        }

        protected XmlNodeList TargetNodes
        {
            get
            {
                return UseParentAsTargetNode ? _context.TargetParents : _context.TargetNodes;
            }
        }


        protected XmlNodeList TargetChildNodes
        {
            get
            {
                return _context.TargetNodes;
            }
        }

        protected XmlTransformationLogger Log
        {
            get
            {
                if (_logger == null)
                {
                    _logger = _context.GetService<XmlTransformationLogger>();
                    if (_logger != null)
                    {
                        _logger.CurrentReferenceNode = _context.TransformAttribute;
                    }
                }
                return _logger;
            }
        }



        protected T GetService<T>() where T : class
        {
            return _context.GetService<T>();
        }

        protected string ArgumentString
        {
            get
            {
                return _argumentString;
            }
        }

        protected IList<string> Arguments
        {
            get
            {
                return _arguments != null || _argumentString == null ? _arguments : XmlArgumentUtility.SplitArguments(_argumentString);
            }
        }

        private string TransformNameLong
        {
            get
            {
                return _context.HasLineInfo ? string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_TransformNameFormatLong, TransformName, _context.TransformLineNumber, _context.TransformLinePosition) : TransformNameShort;
            }
        }

        internal string TransformNameShort
        {
            get
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_TransformNameFormatShort, TransformName);
            }
        }

        private string TransformName
        {
            get
            {
                return GetType().Name;
            }
        }

        internal void Execute(XmlElementContext context, string argumentString)
        {
            Debug.Assert(_context == null && _argumentString == null, "Don't call Execute recursively");
            Debug.Assert(_logger == null, "Logger wasn't released from previous execution");

            if (_context != null || _argumentString != null) return;
            var error = false;
            var startedSection = false;

            try
            {
                _context = context;
                _argumentString = argumentString;
                _arguments = null;

                if (!ShouldExecuteTransform()) return;
                startedSection = true;

                Log.StartSection(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformBeginExecutingMessage, TransformNameLong);
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformStatusXPath, context.XPath);

                if (ApplyTransformToAllTargetNodes)
                {
                    ApplyOnAllTargetNodes();
                }
                else
                {
                    ApplyOnce();
                }
            }
            catch (Exception ex)
            {
                error = true;
                Log.LogErrorFromException(context.TransformAttribute != null ? XmlNodeException.Wrap(ex, context.TransformAttribute) : ex);
            }
            finally
            {
                if (startedSection)
                {
                    var message = error ? SR.XMLTRANSFORMATION_TransformErrorExecutingMessage : SR.XMLTRANSFORMATION_TransformEndExecutingMessage;
                    Log.EndSection(MessageType.Verbose, message, TransformNameShort);
                }
                else
                {
                    Log.LogMessage(MessageType.Normal, SR.XMLTRANSFORMATION_TransformNotExecutingMessage, TransformNameLong);
                }

                _context = null;
                _argumentString = null;
                _arguments = null;

                ReleaseLogger();
            }
        }

        private void ReleaseLogger()
        {
            if (_logger == null) return;
            _logger.CurrentReferenceNode = null;
            _logger = null;
        }

        private bool ApplyOnAllTargetNodes()
        {
            var error = false;
            var originalTransformNode = TransformNode;

            foreach (XmlNode node in TargetNodes)
            {
                try
                {
                    _currentTargetNode = node;
                    _currentTransformNode = originalTransformNode.Clone();

                    ApplyOnce();
                }
                catch (Exception ex)
                {
                    Log.LogErrorFromException(ex);
                    error = true;
                }
            }

            _currentTargetNode = null;

            return error;
        }

        private void ApplyOnce()
        {
            WriteApplyMessage(TargetNode);
            Apply();
        }

        private void WriteApplyMessage(XmlNode targetNode)
        {
            var lineInfo = targetNode as IXmlLineInfo;
            if (lineInfo != null)
            {
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformStatusApplyTarget, targetNode.Name, lineInfo.LineNumber, lineInfo.LinePosition);
            }
            else
            {
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformStatusApplyTargetNoLineInfo, targetNode.Name);
            }
        }

        private bool ShouldExecuteTransform()
        {
            return HasRequiredTarget();
        }

        private bool HasRequiredTarget()
        {
            bool existedInOriginal;
            XmlElementContext matchFailureContext;

            var hasRequiredTarget = UseParentAsTargetNode
                ? _context.HasTargetParent(out matchFailureContext, out existedInOriginal)
                : _context.HasTargetNode(out matchFailureContext, out existedInOriginal);

            if (hasRequiredTarget) return true;
            HandleMissingTarget(matchFailureContext, existedInOriginal);
            return false;
        }

        private void HandleMissingTarget(XmlElementContext matchFailureContext, bool existedInOriginal)
        {
            var messageFormat = existedInOriginal
                ? SR.XMLTRANSFORMATION_TransformSourceMatchWasRemoved
                : SR.XMLTRANSFORMATION_TransformNoMatchingTargetNodes;

            var message = string.Format(System.Globalization.CultureInfo.CurrentCulture, messageFormat, matchFailureContext.XPath);
            switch (MissingTargetMessage)
            {
                case MissingTargetMessage.None:
                    Log.LogMessage(MessageType.Verbose, message);
                    break;
                case MissingTargetMessage.Information:
                    Log.LogMessage(MessageType.Normal, message);
                    break;
                case MissingTargetMessage.Warning:
                    Log.LogWarning(matchFailureContext.Node, message);
                    break;
                case MissingTargetMessage.Error:
                    throw new XmlNodeException(message, matchFailureContext.Node);
            }
        }
    }
}
