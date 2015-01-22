using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.XmlTransform.Extended;

namespace Microsoft.Web.XmlTransform.Test
{
    [TestClass]
    public class XmlTransformTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void XmlTransform_Support_WriteToStream()
        {
            var src = CreateATestFile("Web.config", Properties.Resources.Web);
            var transformFile = CreateATestFile("Web.Release.config", Properties.Resources.Web_Release);
            var destFile = GetTestFilePath("MyWeb.config");

            //execute
            var x = new XmlTransformableDocument {PreserveWhitespace = true};
            x.Load(src);

            var transform = new XmlTransformation(transformFile);

            var succeed = transform.Apply(x);

            var fsDestFile = new FileStream(destFile, FileMode.OpenOrCreate);
            x.Save(fsDestFile);

            //verify, we have a success transform
            Assert.AreEqual(true, succeed);

            //verify, the stream is not closed
            Assert.AreEqual(true, fsDestFile.CanWrite, "The file stream can not be written. was it closed?");

            //sanity verify the content is right, (xml was transformed)
            fsDestFile.Close();
            var content = File.ReadAllText(destFile);
            Assert.IsFalse(content.Contains("debug=\"true\""));
            
            var lines = new List<string>(File.ReadLines(destFile));
            //sanity verify the line format is not lost (otherwsie we will have only one long line)
            Assert.IsTrue(lines.Count>10);

            //be nice 
            transform.Dispose();
            x.Dispose();
        }

        [TestMethod]
        public void XmlTransform_AttibuteFormatting()
        {
            Transform_TestRunner_ExpectSuccess(Properties.Resources.AttributeFormating_source,
                    Properties.Resources.AttributeFormating_transform,
                    Properties.Resources.AttributeFormating_destination,
                    Properties.Resources.AttributeFormatting_log);
        }

        [TestMethod]
        public void XmlTransform_TagFormatting()
        {
             Transform_TestRunner_ExpectSuccess(Properties.Resources.TagFormatting_source,
                    Properties.Resources.TagFormatting_transform,
                    Properties.Resources.TagFormatting_destination,
                    Properties.Resources.TagFormatting_log);
        }

        [TestMethod]
        public void XmlTransform_HandleEdgeCase()
        {
            //2 edge cases we didn't handle well and then fixed it per customer feedback.
            //    a. '>' in the attribute value
            //    b. element with only one character such as <p>
            Transform_TestRunner_ExpectSuccess(Properties.Resources.EdgeCase_source,
                    Properties.Resources.EdgeCase_transform,
                    Properties.Resources.EdgeCase_destination,
                    Properties.Resources.EdgeCase_log);
        }

        [TestMethod]
        public void XmlTransform_ErrorAndWarning()
        {
            Transform_TestRunner_ExpectFail(Properties.Resources.WarningsAndErrors_source,
                    Properties.Resources.WarningsAndErrors_transform,
                    Properties.Resources.WarningsAndErrors_log);
        }

        private void Transform_TestRunner_ExpectSuccess(string source, string transform, string baseline, string expectedLog)
        {
            var src = CreateATestFile("source.config", source);
            var transformFile = CreateATestFile("transform.config", transform);
            var baselineFile = CreateATestFile("baseline.config", baseline);
            var destFile = GetTestFilePath("result.config");
            var logger = new TestTransformationLogger();

            var x = new XmlTransformableDocument {PreserveWhitespace = true};
            x.Load(src);

            var xmlTransform = new XmlTransformation(transformFile, logger);

            //execute
            var succeed = xmlTransform.Apply(x);
            x.Save(destFile);
            xmlTransform.Dispose();
            x.Dispose();
            //test
            Assert.AreEqual(true, succeed);
            CompareFiles(destFile, baselineFile);
            CompareMultiLines(expectedLog, logger.LogText);
        }

        private void Transform_TestRunner_ExpectFail(string source, string transform, string expectedLog)
        {
            var src = CreateATestFile("source.config", source);
            var transformFile = CreateATestFile("transform.config", transform);
            var destFile = GetTestFilePath("result.config");
            var logger = new TestTransformationLogger();

            var x = new XmlTransformableDocument {PreserveWhitespace = true};
            x.Load(src);

            var xmlTransform = new XmlTransformation(transformFile, logger);

            //execute
            var succeed = xmlTransform.Apply(x);
            x.Save(destFile);
            xmlTransform.Dispose();
            x.Dispose();
            //test
            Assert.AreEqual(false, succeed);
            CompareMultiLines(expectedLog, logger.LogText);
        }

        private void CompareFiles(string baseLinePath, string resultPath)
        {
            string bsl;
            using (var sr = new StreamReader(baseLinePath))
            {
                bsl = sr.ReadToEnd();
            }

            string result;
            using (var sr = new StreamReader(resultPath))
            {
                result = sr.ReadToEnd();
            }

            CompareMultiLines(bsl, result);
        }

        private void CompareMultiLines(string baseline, string result)
        {
            var baseLines = baseline.Split(new[] { Environment.NewLine },  StringSplitOptions.None);
            var resultLines = result.Split(new[] { Environment.NewLine },  StringSplitOptions.None);

            for (var i = 0; i < baseLines.Length; i++)
            {
                Assert.AreEqual(baseLines[i], resultLines[i], string.Format("line {0} at baseline file is not matched", i));
            }
        }

        private string CreateATestFile(string filename, string contents)
        {
            var file = GetTestFilePath(filename);
            File.WriteAllText(file, contents);
            return file;
        }

        private string GetTestFilePath(string filename)
        {
            var folder = Path.Combine(TestContext.TestDeploymentDir, TestContext.TestName);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, filename);
            return file;
        }
    }
}
