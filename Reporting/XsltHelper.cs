﻿using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Xsl;
using TechTalk.SpecFlow.Generator.Configuration;

namespace TechTalk.SpecFlow.Reporting
{
    internal static class XsltHelper
    {
        public static bool IsXmlOutput(string outputFilePath)
        {
            return Path.GetExtension(outputFilePath).Equals(".xml", StringComparison.InvariantCultureIgnoreCase);
        }

        public static void TransformXml(XmlSerializer serializer, object report, string outputFilePath)
        {
            string xmlOutputPath = Path.ChangeExtension(outputFilePath, ".xml");

            using (var writer = new StreamWriter(xmlOutputPath, false, Encoding.UTF8))
            {
                serializer.Serialize(writer, report);
            }
        }

        public static void TransformHtml(XmlSerializer serializer, object report, Type reportType, string outputFilePath, GeneratorConfiguration generatorConfiguration, string xsltFile)
        {
            var xmlOutputWriter = new StringWriter();
            serializer.Serialize(xmlOutputWriter, report);

            XslCompiledTransform xslt = new XslCompiledTransform();
            var xsltSettings = new XsltSettings(true, false);
            XmlResolver resourceResolver;

            var reportName = reportType.Name.Replace("Generator", "");
            using (var xsltReader = GetTemplateReader(reportType, reportName, xsltFile))
            {
                resourceResolver = new XmlResourceResolver();
                xslt.Load(xsltReader, xsltSettings, resourceResolver);
            }

            var xmlOutputReader = new XmlTextReader(new StringReader(xmlOutputWriter.ToString()));

            XsltArgumentList argumentList = new XsltArgumentList();
            argumentList.AddParam("feature-language", "", generatorConfiguration.FeatureLanguage.Name);
            argumentList.AddParam("tool-language", "", generatorConfiguration.ToolLanguage.Name);
            using (var outFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
            {
                xslt.Transform(xmlOutputReader, argumentList, outFileStream, resourceResolver);
            }            
        }

        private static XmlReader GetTemplateReader(Type reportType, string reportName, string xsltFile)
        {
            if (string.IsNullOrEmpty(xsltFile))
                return new ResourceXmlReader(reportType, reportName + ".xslt");

            return new XmlTextReader(xsltFile);
        }

        private static void Transform(this XslCompiledTransform xslt, XmlReader input, XsltArgumentList arguments, Stream results, XmlResolver documentResolver)
        {
            var command = xslt.GetType().GetField("command", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(xslt);

            var executeMethod = command.GetType().GetMethod("Execute", new Type[] { typeof(XmlReader), typeof(XmlResolver), typeof(XsltArgumentList), typeof(Stream) });

            try
            {
                executeMethod.Invoke(command, new object[] {input, documentResolver, arguments, results});
            }
            catch (TargetInvocationException invEx)
            {
                var ex = invEx.InnerException;
                ex.PreserveStackTrace();
                throw ex;
            }
        }
    }
}
