﻿//------------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="ConnectorDocumenter.cs" company="Microsoft">
//      Copyright (c) Microsoft. All Rights Reserved.
//      Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
// </copyright>
// <summary>
// Azure AD Connect Sync Connector Configuration Documenter
// </summary>
//------------------------------------------------------------------------------------------------------------------------------------------

namespace AzureADConnectSyncDocumenter
{
    using System;
    using System.Collections.Specialized;
    using System.Data;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Web.UI;
    using System.Xml.Linq;
    using System.Xml.XPath;

    /// <summary>
    /// The ConnectorDocumenter is an abstract base class for all types of AAD Connect sync Connectors.
    /// </summary>
    internal abstract class ConnectorDocumenter : Documenter
    {
        /// <summary>
        /// The logger context item connector name
        /// </summary>
        protected const string LoggerContextItemConnectorName = "Connector Name";

        /// <summary>
        /// The logger context item connector unique identifier
        /// </summary>
        protected const string LoggerContextItemConnectorGuid = "Connector Guid";

        /// <summary>
        /// The logger context item connector category
        /// </summary>
        protected const string LoggerContextItemConnectorCategory = "Connector Category";

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectorDocumenter" /> class.
        /// </summary>
        /// <param name="pilotXml">The pilot configuration XML.</param>
        /// <param name="productionXml">The production configuration XML.</param>
        /// <param name="connectorName">The connector name.</param>
        /// <param name="productionOnly">if set to <c>true</c> [production only].</param>
        protected ConnectorDocumenter(XElement pilotXml, XElement productionXml, string connectorName, bool productionOnly)
        {
            Logger.Instance.WriteMethodEntry("Connector Name: '{0}'. Production Only: '{1}'.", connectorName, productionOnly);

            try
            {
                this.PilotXml = pilotXml;
                this.ProductionXml = productionXml;
                this.ConnectorName = connectorName;

                string xpath = "//ma-data[name ='" + this.ConnectorName + "']";
                var connector = productionOnly ? this.ProductionXml.XPathSelectElement(xpath, Documenter.NamespaceManager) : this.PilotXml.XPathSelectElement(xpath, Documenter.NamespaceManager);

                this.ConnectorGuid = (string)connector.Element("id");
                this.ConnectorCategory = (string)connector.Element("category");
                this.ConnectorSubType = (string)connector.Element("subtype");

                // Set Logger call context items
                Logger.SetContextItem(ConnectorDocumenter.LoggerContextItemConnectorName, this.ConnectorName);
                Logger.SetContextItem(ConnectorDocumenter.LoggerContextItemConnectorGuid, this.ConnectorGuid);
                Logger.SetContextItem(ConnectorDocumenter.LoggerContextItemConnectorCategory, this.ConnectorCategory);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Gets the name of the connector.
        /// </summary>
        /// <value>
        /// The name of the connector.
        /// </value>
        public string ConnectorName { get; private set; }

        /// <summary>
        /// Gets the connector unique identifier.
        /// </summary>
        /// <value>
        /// The connector unique identifier.
        /// </value>
        public string ConnectorGuid { get; private set; }

        /// <summary>
        /// Gets the connector category.
        /// </summary>
        /// <value>
        /// The connector category.
        /// </value>
        public string ConnectorCategory { get; private set; }

        /// <summary>
        /// Gets the type of the connector sub.
        /// </summary>
        /// <value>
        /// The type of the connector sub.
        /// </value>
        public string ConnectorSubType { get; private set; }

        /// <summary>
        /// Gets the connector configuration report.
        /// </summary>
        /// <returns>
        /// The Tuple of configuration report and associated TOC
        /// </returns>
        public override Tuple<string, string> GetReport()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                this.ReportWriter.Close();
                this.ReportToCWriter.Close();

                string report;
                string toc;

                using (var reportReader = new StreamReader(this.ReportFileName))
                {
                    report = reportReader.ReadToEnd();
                    using (var tocReader = new StreamReader(this.ReportToCFileName))
                    {
                        toc = tocReader.ReadToEnd();
                    }
                }

                return new Tuple<string, string>(report, toc);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Gets the sync rule xpath.
        /// </summary>
        /// <param name="currentConnectorGuid">The current connector unique identifier.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="reportType">Type of the report.</param>
        /// <returns>The sync rule xpath.</returns>
        protected static string GetSyncRuleXPath(string currentConnectorGuid, SyncRuleDocumenter.SyncRuleDirection direction, SyncRuleDocumenter.SyncRuleReportType reportType)
        {
            Logger.Instance.WriteMethodEntry("Current Connector Guid: '{0}'. Sync Rule Direction: '{1}'.  Sync Rule Report Type: '{2}'.", currentConnectorGuid, direction, reportType);

            var xpath = "//synchronizationRule[translate(connector, '" + Documenter.LowercaseLetters + "', '" + Documenter.UppercaseLetters + "') = '" + currentConnectorGuid + "' and direction = '" + direction.ToString() + "'";

            try
            {
                switch (reportType)
                {
                    case SyncRuleDocumenter.SyncRuleReportType.ProvisioningSection:
                        xpath += " and linkType = 'Provision'";
                        break;
                    case SyncRuleDocumenter.SyncRuleReportType.StickyJoinSection:
                        xpath += " and linkType = 'StickyJoin'";
                        break;
                    case SyncRuleDocumenter.SyncRuleReportType.ConditionalJoinSection:
                        xpath += " and linkType = 'Join'";
                        xpath += " and (count(synchronizationCriteria/conditions/scope) != 0 or  count(relationshipCriteria/conditions/condition) != 0)";
                        break;
                }

                xpath += "]";

                return xpath;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Current Connector Guid: '{0}'. Sync Rule Direction: '{1}'.  Sync Rule Report Type: '{2}'. XPath: '{3}'.", currentConnectorGuid, direction, reportType, xpath);
            }
        }

        /// <summary>
        /// Gets the sync rule section title.
        /// </summary>
        /// <param name="reportType">Type of the report.</param>
        /// <returns>
        /// The sync rule section title.
        /// </returns>
        protected static string GetSyncRuleSectionTitle(SyncRuleDocumenter.SyncRuleReportType reportType)
        {
            Logger.Instance.WriteMethodEntry("Sync Rule Report Type: '{0}'.", reportType);

            var sectionTitle = "Synchronization Rules";

            try
            {
                switch (reportType)
                {
                    case SyncRuleDocumenter.SyncRuleReportType.ProvisioningSection:
                        sectionTitle = "Provisioning Rules Summary";
                        break;
                    case SyncRuleDocumenter.SyncRuleReportType.StickyJoinSection:
                        sectionTitle = "Sticky Join Rules Summary";
                        break;
                    case SyncRuleDocumenter.SyncRuleReportType.ConditionalJoinSection:
                        sectionTitle = "Conditional Join Rules Summary";
                        break;
                }

                return sectionTitle;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Sync Rule Report Type: '{0}'. Sync Rule Section Title: '{1}'.", reportType, sectionTitle);
            }
        }

        /// <summary>
        /// Gets the type of the run profile step.
        /// </summary>
        /// <param name="runProfileStepType">Type of the run profile step.</param>
        /// <returns>The type of the run profile step.</returns>
        protected static string GetRunProfileStepType(XElement runProfileStepType)
        {
            Logger.Instance.WriteMethodEntry("Sync Rule Report Type: '{0}'.", runProfileStepType != null ? (string)runProfileStepType.Attribute("type") : null);

            var stepType = string.Empty;

            try
            {
                if (runProfileStepType != null)
                {
                    var type = ((string)runProfileStepType.Attribute("type") ?? string.Empty).ToUpperInvariant();
                    switch (type)
                    {
                        case "DELTA-IMPORT":
                            {
                                var importType = ((string)runProfileStepType.Element("import-subtype") ?? string.Empty).ToUpperInvariant();
                                stepType = importType == "TO-CS" ? "Delta Import (Stage Only)" : "Delta Import and Delta Synchronization";
                                break;
                            }

                        case "FULL-IMPORT":
                            {
                                var importType = ((string)runProfileStepType.Element("import-subtype") ?? string.Empty).ToUpperInvariant();
                                stepType = importType == "TO-CS" ? "Full Import (Stage Only)" : "Full Import and Delta Synchronization";
                                break;
                            }

                        case "EXPORT":
                            {
                                stepType = "Export";
                                break;
                            }

                        case "FULL-IMPORT-REEVALUATE-RULES":
                            {
                                stepType = "Full Import and Full Synchronization";
                                break;
                            }

                        case "APPLY-RULES":
                            {
                                var subType = ((string)runProfileStepType.Element("apply-rules-subtype") ?? string.Empty).ToUpperInvariant();
                                stepType = subType == "APPLY-PENDING" ? "Delta Synchronization" : subType == "REEVALUATE-FLOW-CONNECTORS" ? "Full Synchronization" : subType;
                                break;
                            }

                        default:
                            {
                                stepType = type;
                                break;
                            }
                    }
                }

                return stepType;
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Sync Rule Report Type: '{0}'.", stepType);
            }
        }

        /// <summary>
        /// Writes the connector report header.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Reviewed. XhtmlTextWriter takes care of disposting StreamWriter.")]
        protected void WriteConnectorReportHeader()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                this.ReportWriter = new XhtmlTextWriter(new StreamWriter(this.ReportFileName));
                this.ReportToCWriter = new XhtmlTextWriter(new StreamWriter(this.ReportToCFileName));

                var bookmarkCode = this.ConnectorName;
                var bookmarkText = this.ConnectorName + " Connector Configuration";

                Logger.Instance.WriteInfo("Processing " + bookmarkText + ".");

                #region toc

                this.ReportToCWriter.WriteBeginTag("span");
                this.ReportToCWriter.WriteAttribute("class", "toc2");
                this.ReportToCWriter.Write(HtmlTextWriter.TagRightChar);
                Documenter.WriteJumpToBookmarkLocation(this.ReportToCWriter, bookmarkCode, bookmarkText, this.ConnectorGuid, "TOC");
                this.ReportToCWriter.WriteEndTag("span");
                this.ReportToCWriter.WriteBeginTag("br");
                this.ReportToCWriter.Write(HtmlTextWriter.SelfClosingTagEnd);
                this.ReportToCWriter.WriteLine();

                #endregion toc

                #region section

                this.ReportWriter.WriteFullBeginTag("h2");
                Documenter.WriteBookmarkLocation(this.ReportWriter, bookmarkCode, bookmarkText, this.ConnectorGuid, "TOC");
                this.ReportWriter.WriteEndTag("h2");

                #endregion section
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Writes the connector synchronize rules header.
        /// </summary>
        /// <param name="reportType">Type of the report.</param>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void WriteConnectorSyncRulesHeader(SyncRuleDocumenter.SyncRuleReportType reportType)
        {
            Logger.Instance.WriteMethodEntry("Sync Rule Report Type: '{0}'.", reportType);

            try
            {
                var sectionTitle = ConnectorDocumenter.GetSyncRuleSectionTitle(reportType);

                Logger.Instance.WriteInfo("Processing " + sectionTitle + ".");

                #region toc

                this.ReportToCWriter.WriteBeginTag("span");
                this.ReportToCWriter.WriteAttribute("class", "toc3");
                this.ReportToCWriter.Write(HtmlTextWriter.TagRightChar);
                Documenter.WriteJumpToBookmarkLocation(this.ReportToCWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportToCWriter.WriteEndTag("span");
                this.ReportToCWriter.WriteBeginTag("br");
                this.ReportToCWriter.Write(HtmlTextWriter.SelfClosingTagEnd);
                this.ReportToCWriter.WriteLine();

                #endregion toc

                #region section

                this.ReportWriter.WriteFullBeginTag("h3");
                Documenter.WriteBookmarkLocation(this.ReportWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportWriter.WriteEndTag("h3");

                #endregion section
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Sync Rule Report Type: '{0}'.", reportType);
            }
        }

        #region Connector Properties

        /// <summary>
        /// Processes the connector properties.
        /// </summary>
        protected void ProcessConnectorProperties()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                Logger.Instance.WriteInfo("Processing Connector Properties.");

                this.CreateSimpleOrderedSettingsDataSets(3); // 1 = Display Order, 2 = Property Name, 3 = Value

                this.FillConnectorPropertiesDataSet(true);
                this.FillConnectorPropertiesDataSet(false);

                this.CreateSimpleOrderedSettingsDiffgram();

                this.PrintConnectorProperties();
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Fills the connector properties data set.
        /// </summary>
        /// <param name="pilotConfig">if set to <c>true</c>, the pilot configuration is loaded. Otherwise, the production configuration is loaded.</param>
        protected void FillConnectorPropertiesDataSet(bool pilotConfig)
        {
            Logger.Instance.WriteMethodEntry("Pilot Config: '{0}'.", pilotConfig);

            try
            {
                var config = pilotConfig ? this.PilotXml : this.ProductionXml;
                var dataSet = pilotConfig ? this.PilotDataSet : this.ProductionDataSet;

                var table = dataSet.Tables[0];

                var connector = config.XPathSelectElement("//ma-data[name ='" + this.ConnectorName + "']");

                if (connector != null)
                {
                    var setting = (string)connector.Element("name");
                    Documenter.AddRow(table, new object[] { 0, "Connector Name", setting });

                    setting = (string)connector.Element("category");
                    Documenter.AddRow(table, new object[] { 1, "Connector Type", setting });

                    setting = (string)connector.Element("description");
                    Documenter.AddRow(table, new object[] { 2, "Description", setting });

                    setting = (string)connector.Element("subtype");
                    if (!string.IsNullOrEmpty(setting))
                    {
                        Documenter.AddRow(table, new object[] { 3, "Sub Type", setting });
                    }

                    setting = (string)connector.Element("ma-listname");
                    if (!string.IsNullOrEmpty(setting))
                    {
                        Documenter.AddRow(table, new object[] { 4, "List Name", setting });
                    }

                    setting = (string)connector.Element("ma-companyname");
                    if (!string.IsNullOrEmpty(setting))
                    {
                        Documenter.AddRow(table, new object[] { 5, "Company", setting });
                    }

                    setting = (string)connector.Element("creation-time");
                    Documenter.AddRow(table, new object[] { 6, "Creation Time", setting });

                    setting = (string)connector.Element("last-modification-time");
                    Documenter.AddRow(table, new object[] { 7, "Last Modification Time", setting });

                    table.AcceptChanges();
                }
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Pilot Config: '{0}'.", pilotConfig);
            }
        }

        /// <summary>
        /// Prints the connector properties.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void PrintConnectorProperties()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var sectionTitle = "Connector Properties";

                #region toc

                this.ReportToCWriter.WriteBeginTag("span");
                this.ReportToCWriter.WriteAttribute("class", "toc3");
                this.ReportToCWriter.Write(HtmlTextWriter.TagRightChar);
                Documenter.WriteJumpToBookmarkLocation(this.ReportToCWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportToCWriter.WriteEndTag("span");
                this.ReportToCWriter.WriteBeginTag("br");
                this.ReportToCWriter.Write(HtmlTextWriter.SelfClosingTagEnd);
                this.ReportToCWriter.WriteLine();

                #endregion toc

                #region section

                this.ReportWriter.WriteFullBeginTag("h3");
                Documenter.WriteBookmarkLocation(this.ReportWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportWriter.WriteEndTag("h3");

                #endregion section

                #region table

                this.ReportWriter.WriteBeginTag("table");
                this.ReportWriter.WriteAttribute("class", "outer-table");
                this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                {
                    #region thead

                    this.ReportWriter.WriteBeginTag("thead");
                    this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                    {
                        this.ReportWriter.WriteBeginTag("tr");
                        this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                        {
                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Setting");
                            this.ReportWriter.WriteEndTag("th");

                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Configuration");
                            this.ReportWriter.WriteEndTag("th");
                        }

                        this.ReportWriter.WriteEndTag("tr");
                        this.ReportWriter.WriteLine();
                    }

                    this.ReportWriter.WriteEndTag("thead");

                    #endregion thead
                }

                #region rows

                this.WriteRows(this.DiffgramDataSet.Tables[0].Rows.Cast<DataRow>().ToArray(), 0, 0);

                #endregion rows

                this.ReportWriter.WriteEndTag("table");

                #endregion table
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Connector Properties

        #region Provisioning Hierarchy

        /// <summary>
        /// Processes the connector provisioning hierarchy configuration.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void ProcessConnectorProvisioningHierarchyConfiguration()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                Logger.Instance.WriteInfo("Processing Provisioning Hierarchy Configuration.");

                this.CreateSimpleSettingsDataSets(2); // 1 = DN Component, 2 = Object Class Mapping

                this.FillConnectorProvisioningHierarchyConfigurationDataSet(true);
                this.FillConnectorProvisioningHierarchyConfigurationDataSet(false);

                this.CreateSimpleSettingsDiffgram();

                this.PrintConnectorProvisioningHierarchyConfiguration();
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Fills the connector provisioning hierarchy configuration data set.
        /// </summary>
        /// <param name="pilotConfig">if set to <c>true</c>, the pilot configuration is loaded. Otherwise, the production configuration is loaded.</param>
        protected void FillConnectorProvisioningHierarchyConfigurationDataSet(bool pilotConfig)
        {
            Logger.Instance.WriteMethodEntry("Pilot Config: '{0}'.", pilotConfig);

            try
            {
                var config = pilotConfig ? this.PilotXml : this.ProductionXml;
                var dataSet = pilotConfig ? this.PilotDataSet : this.ProductionDataSet;

                var table = dataSet.Tables[0];

                var mappings = config.XPathSelectElements("//ma-data[name ='" + this.ConnectorName + "']/component_mappings/mapping");

                foreach (var mapping in mappings)
                {
                    Documenter.AddRow(table, new object[] { (string)mapping.Element("dn_component"), (string)mapping.Element("object_class") });
                }

                table.AcceptChanges();
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Pilot Config: '{0}'.", pilotConfig);
            }
        }

        /// <summary>
        /// Prints the connector provisioning hierarchy configuration.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void PrintConnectorProvisioningHierarchyConfiguration()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var sectionTitle = "Provisioning Hierarchy";

                #region toc

                this.ReportToCWriter.WriteBeginTag("span");
                this.ReportToCWriter.WriteAttribute("class", "toc3");
                this.ReportToCWriter.Write(HtmlTextWriter.TagRightChar);
                Documenter.WriteJumpToBookmarkLocation(this.ReportToCWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportToCWriter.WriteEndTag("span");
                this.ReportToCWriter.WriteBeginTag("br");
                this.ReportToCWriter.Write(HtmlTextWriter.SelfClosingTagEnd);
                this.ReportToCWriter.WriteLine();

                #endregion toc

                #region section

                this.ReportWriter.WriteFullBeginTag("h3");
                Documenter.WriteBookmarkLocation(this.ReportWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportWriter.WriteEndTag("h3");

                #endregion section

                if (this.DiffgramDataSet.Tables[0].Rows.Count == 0)
                {
                    this.ReportWriter.WriteLine();
                    this.ReportWriter.WriteLine("The provisioning hierarchy is not enabled.");

                    return;
                }

                #region table

                this.ReportWriter.WriteBeginTag("table");
                this.ReportWriter.WriteAttribute("class", "outer-table");
                this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                {
                    #region thead

                    this.ReportWriter.WriteBeginTag("thead");
                    this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                    {
                        this.ReportWriter.WriteBeginTag("tr");
                        this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                        {
                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("DN Component");
                            this.ReportWriter.WriteEndTag("th");

                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Object Class Mapping");
                            this.ReportWriter.WriteEndTag("th");
                        }

                        this.ReportWriter.WriteEndTag("tr");
                        this.ReportWriter.WriteLine();
                    }

                    this.ReportWriter.WriteEndTag("thead");

                    #endregion thead
                }

                #region rows

                this.WriteRows(this.DiffgramDataSet.Tables[0].Rows.Cast<DataRow>().ToArray(), 0, 0);

                #endregion rows

                this.ReportWriter.WriteEndTag("table");

                #endregion table
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Provisioning Hierarchy

        #region Selected Object Types

        /// <summary>
        /// Processes the selected object types.
        /// </summary>
        protected void ProcessConnectorSelectedObjectTypes()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                Logger.Instance.WriteInfo("Processing Selected Object Types.");

                this.CreateSimpleSettingsDataSets(1);

                this.FillConnectorSelectedObjectTypesDataSet(true);
                this.FillConnectorSelectedObjectTypesDataSet(false);

                this.CreateSimpleSettingsDiffgram();

                this.PrintConnectorSelectedObjectTypes();
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Fills the selected object types data set.
        /// </summary>
        /// <param name="pilotConfig">if set to <c>true</c>, the pilot configuration is loaded. Otherwise, the production configuration is loaded.</param>
        protected void FillConnectorSelectedObjectTypesDataSet(bool pilotConfig)
        {
            Logger.Instance.WriteMethodEntry("Pilot Config: '{0}'", pilotConfig);

            try
            {
                var config = pilotConfig ? this.PilotXml : this.ProductionXml;
                var dataSet = pilotConfig ? this.PilotDataSet : this.ProductionDataSet;

                var table = dataSet.Tables[0];

                var objectTypes = config.XPathSelectElements("//ma-data[name ='" + this.ConnectorName + "']/ma-partition-data/partition[position() = 1]/filter/object-classes/object-class");

                foreach (var objectType in objectTypes)
                {
                    Documenter.AddRow(table, new object[] { (string)objectType });
                }

                table.AcceptChanges();
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Pilot Config: '{0}'", pilotConfig);
            }
        }

        /// <summary>
        /// Prints the selected object types.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void PrintConnectorSelectedObjectTypes()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var sectionTitle = "Selected Object Types";

                #region toc

                this.ReportToCWriter.WriteBeginTag("span");
                this.ReportToCWriter.WriteAttribute("class", "toc3");
                this.ReportToCWriter.Write(HtmlTextWriter.TagRightChar);
                Documenter.WriteJumpToBookmarkLocation(this.ReportToCWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportToCWriter.WriteEndTag("span");
                this.ReportToCWriter.WriteBeginTag("br");
                this.ReportToCWriter.Write(HtmlTextWriter.SelfClosingTagEnd);
                this.ReportToCWriter.WriteLine();

                #endregion toc

                #region section

                this.ReportWriter.WriteFullBeginTag("h3");
                Documenter.WriteBookmarkLocation(this.ReportWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportWriter.WriteEndTag("h3");

                #endregion section

                #region table

                this.ReportWriter.WriteBeginTag("table");
                this.ReportWriter.WriteAttribute("class", "outer-table");
                this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                {
                    #region thead

                    this.ReportWriter.WriteBeginTag("thead");
                    this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                    {
                        this.ReportWriter.WriteBeginTag("tr");
                        this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                        {
                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Object Types");
                            this.ReportWriter.WriteEndTag("th");
                        }

                        this.ReportWriter.WriteEndTag("tr");
                        this.ReportWriter.WriteLine();
                    }

                    this.ReportWriter.WriteEndTag("thead");

                    #endregion thead
                }

                #region rows

                this.WriteRows(this.DiffgramDataSet.Tables[0].Rows.Cast<DataRow>().ToArray(), 0, 0);

                #endregion rows

                this.ReportWriter.WriteEndTag("table");

                #endregion table
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Selected Object Types

        #region Selected Attributes

        /// <summary>
        /// Processes the connector selected attributes.
        /// </summary>
        protected void ProcessConnectorSelectedAttributes()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                Logger.Instance.WriteInfo("Processing Selected Attributes.");

                this.CreateSimpleSettingsDataSets(4);

                this.FillConnectorSelectedAttributesDataSet(true);
                this.FillConnectorSelectedAttributesDataSet(false);

                this.CreateSimpleSettingsDiffgram();

                this.PrintConnectorSelectedAttributes();
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Fills the connector selected attributes data set.
        /// </summary>
        /// <param name="pilotConfig">if set to <c>true</c>, the pilot configuration is loaded. Otherwise, the production configuration is loaded.</param>
        protected void FillConnectorSelectedAttributesDataSet(bool pilotConfig)
        {
            Logger.Instance.WriteMethodEntry("Pilot Config: '{0}'.", pilotConfig);

            try
            {
                var config = pilotConfig ? this.PilotXml : this.ProductionXml;
                var dataSet = pilotConfig ? this.PilotDataSet : this.ProductionDataSet;

                var connector = config.XPathSelectElement("//ma-data[name ='" + this.ConnectorName + "']");

                if (connector != null)
                {
                    var table = dataSet.Tables[0];
                    var currentConnectorGuid = ((string)connector.Element("id") ?? string.Empty).ToUpperInvariant(); // This may be pilot or production GUID

                    foreach (var attribute in connector.XPathSelectElements("attribute-inclusion/attribute"))
                    {
                        var attributeName = (string)attribute;

                        var attributeInfo = connector.XPathSelectElement("//dsml:attribute-type[dsml:name = '" + attributeName + "']", Documenter.NamespaceManager);

                        var inboundSyncRules = config.XPathSelectElements("//synchronizationRule[translate(connector, '" + Documenter.LowercaseLetters + "', '" + Documenter.UppercaseLetters + "') = '" + currentConnectorGuid + "' and direction = 'Inbound']/attribute-mappings/mapping/src[attr = '" + attributeName + "']");
                        var outboundSyncRules = config.XPathSelectElements("//synchronizationRule[translate(connector, '" + Documenter.LowercaseLetters + "', '" + Documenter.UppercaseLetters + "') = '" + currentConnectorGuid + "' and direction = 'Outbound']/attribute-mappings/mapping/src[attr = '" + attributeName + "']");

                        var hasInboundFlows = inboundSyncRules.Count() != 0;
                        var hasOutboundFlows = outboundSyncRules.Count() != 0;

                        var row = table.NewRow();

                        row[0] = attributeName;
                        var attributeSyntax = (string)attributeInfo.Element(Documenter.DsmlNamespace + "syntax");
                        row[1] = Documenter.GetAttributeType(attributeSyntax, (string)attributeInfo.Attribute(Documenter.MmsDsmlNamespace + "indexable"));
                        row[2] = ((string)attributeInfo.Attribute("single-value") ?? string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase) ? "No" : "Yes";

                        row[3] = hasInboundFlows && hasOutboundFlows ? "Import / Export" : hasInboundFlows ? "Import" : hasOutboundFlows ? "Export" : "No";

                        Documenter.AddRow(table, row);
                    }

                    table.AcceptChanges();
                }
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Pilot Config: '{0}'", pilotConfig);
            }
        }

        /// <summary>
        /// Prints the connector selected attributes.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void PrintConnectorSelectedAttributes()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var sectionTitle = "Selected Attributes";

                #region toc

                this.ReportToCWriter.WriteBeginTag("span");
                this.ReportToCWriter.WriteAttribute("class", "toc3");
                this.ReportToCWriter.Write(HtmlTextWriter.TagRightChar);
                Documenter.WriteJumpToBookmarkLocation(this.ReportToCWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportToCWriter.WriteEndTag("span");
                this.ReportToCWriter.WriteBeginTag("br");
                this.ReportToCWriter.Write(HtmlTextWriter.SelfClosingTagEnd);
                this.ReportToCWriter.WriteLine();

                #endregion toc

                #region section

                this.ReportWriter.WriteFullBeginTag("h3");
                Documenter.WriteBookmarkLocation(this.ReportWriter, sectionTitle, this.ConnectorGuid, "TOC");
                this.ReportWriter.WriteEndTag("h3");

                #endregion section

                #region table

                this.ReportWriter.WriteBeginTag("table");
                this.ReportWriter.WriteAttribute("class", "outer-table");
                this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                {
                    #region thead

                    this.ReportWriter.WriteBeginTag("thead");
                    this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                    {
                        this.ReportWriter.WriteBeginTag("tr");
                        this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                        {
                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Attribute Name");
                            this.ReportWriter.WriteEndTag("th");

                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Type");
                            this.ReportWriter.WriteEndTag("th");

                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Multi-valued");
                            this.ReportWriter.WriteEndTag("th");

                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Flows Configured?");
                            this.ReportWriter.WriteEndTag("th");
                        }

                        this.ReportWriter.WriteEndTag("tr");
                        this.ReportWriter.WriteLine();
                    }

                    this.ReportWriter.WriteEndTag("thead");

                    #endregion thead
                }

                #region rows

                this.WriteRows(this.DiffgramDataSet.Tables[0].Rows.Cast<DataRow>().ToArray(), 0, 0);

                #endregion rows

                this.ReportWriter.WriteEndTag("table");

                #endregion table
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Selected Atrributes

        #region Provisioning Sync Rules

        /// <summary>
        /// Processes the connector provisioning synchronization rules.
        /// </summary>
        protected void ProcessConnectorProvisioningSyncRules()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var reportType = SyncRuleDocumenter.SyncRuleReportType.ProvisioningSection;
                this.WriteConnectorSyncRulesHeader(reportType);

                this.ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection.Inbound, reportType);
                this.ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection.Outbound, reportType);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Provisioning Sync Rules

        #region Sticky Join Sync Rules

        /// <summary>
        /// Processes the connector sticky join synchronization rules.
        /// </summary>
        protected void ProcessConnectorStickyJoinSyncRules()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var reportType = SyncRuleDocumenter.SyncRuleReportType.StickyJoinSection;
                this.WriteConnectorSyncRulesHeader(reportType);

                this.ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection.Inbound, reportType);
                this.ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection.Outbound, reportType);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Sticky Join Rules

        #region Normal Join Sync Rules

        /// <summary>
        /// Processes the connector normal join sync rules.
        /// </summary>
        protected void ProcessConnectorNormalJoinSyncRules()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var reportType = SyncRuleDocumenter.SyncRuleReportType.ConditionalJoinSection;
                this.WriteConnectorSyncRulesHeader(reportType);

                this.ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection.Inbound, reportType);
                this.ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection.Outbound, reportType);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Normal Join Rules

        #region Sync Rules

        /// <summary>
        /// Processes the connector synchronization rules.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void ProcessConnectorSyncRules()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var reportType = SyncRuleDocumenter.SyncRuleReportType.AllSections;
                this.WriteConnectorSyncRulesHeader(reportType);

                this.ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection.Inbound, reportType);
                this.ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection.Outbound, reportType);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Processes the connector synchronization rules.
        /// </summary>
        /// <param name="direction">The sync rule direction.</param>
        /// <param name="reportType">Type of the report.</param>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void ProcessConnectorSyncRules(SyncRuleDocumenter.SyncRuleDirection direction, SyncRuleDocumenter.SyncRuleReportType reportType)
        {
            Logger.Instance.WriteMethodEntry("Sync Rule Direction: '{0}'. Sync Rule Report Type: '{1}'.", direction, reportType);

            try
            {
                var sectionTitle = ConnectorDocumenter.GetSyncRuleSectionTitle(reportType);

                #region toc

                this.ReportToCWriter.WriteBeginTag("span");
                this.ReportToCWriter.WriteAttribute("class", "toc4");
                this.ReportToCWriter.Write(HtmlTextWriter.TagRightChar);
                Documenter.WriteJumpToBookmarkLocation(this.ReportToCWriter, direction.ToString() + sectionTitle, direction.ToString(), this.ConnectorGuid, "TOC");
                this.ReportToCWriter.WriteEndTag("span");
                this.ReportToCWriter.WriteBeginTag("br");
                this.ReportToCWriter.Write(HtmlTextWriter.SelfClosingTagEnd);
                this.ReportToCWriter.WriteLine();

                #endregion toc

                #region section

                this.ReportWriter.WriteFullBeginTag("h4");
                Documenter.WriteBookmarkLocation(this.ReportWriter, direction.ToString() + sectionTitle, direction.ToString(), this.ConnectorGuid, "TOC");
                this.ReportWriter.WriteEndTag("h4");

                #endregion section

                var sectionPrinted = false;
                var pilotConnector = this.PilotXml.XPathSelectElement("//ma-data[name ='" + this.ConnectorName + "']");
                var productionConnector = this.ProductionXml.XPathSelectElement("//ma-data[name ='" + this.ConnectorName + "']");

                var pilotConnectorGuid = pilotConnector != null ? ((string)pilotConnector.Element("id") ?? string.Empty).ToUpperInvariant() : string.Empty;
                var productionConnectorGuid = productionConnector != null ? ((string)productionConnector.Element("id") ?? string.Empty).ToUpperInvariant() : string.Empty;

                var pilotSyncRules = this.PilotXml.XPathSelectElements(ConnectorDocumenter.GetSyncRuleXPath(pilotConnectorGuid, direction, reportType));
                var productionSyncRules = this.ProductionXml.XPathSelectElements(ConnectorDocumenter.GetSyncRuleXPath(productionConnectorGuid, direction, reportType));

                // Sort by name
                pilotSyncRules = from syncRule in pilotSyncRules
                                 let name = (string)syncRule.Element("name")
                                 orderby name
                                 select syncRule;

                sectionPrinted = pilotSyncRules.Count() != 0;

                foreach (var syncRule in pilotSyncRules)
                {
                    var syncRuleName = (string)syncRule.Element("name");
                    var syncRuleGuid = (string)syncRule.Element("id");
                    var connectorDocumenter = new SyncRuleDocumenter(this.PilotXml, this.ProductionXml, syncRuleName, syncRuleGuid, this.ConnectorName, false);
                    var report = connectorDocumenter.GetReport(reportType);
                    this.ReportWriter.Write(report.Item1);
                    this.ReportToCWriter.Write(report.Item2);
                }

                // Sort by name
                productionSyncRules = from syncRule in productionSyncRules
                                      let name = (string)syncRule.Element("name")
                                      orderby name
                                      select syncRule;

                var pilotSyncRulesNames = from syncRule in pilotSyncRules
                                          select (string)syncRule.Element("name");

                productionSyncRules = productionSyncRules.Where(productionSyncRule => !pilotSyncRulesNames.Contains((string)productionSyncRule.Element("name")));

                sectionPrinted = sectionPrinted || productionSyncRules.Count() != 0;

                foreach (var syncRule in productionSyncRules)
                {
                    var syncRuleName = (string)syncRule.Element("name");
                    var syncRuleGuid = (string)syncRule.Element("id");
                    var connectorDocumenter = new SyncRuleDocumenter(this.PilotXml, this.ProductionXml, syncRuleName, syncRuleGuid, this.ConnectorName, true);
                    var report = connectorDocumenter.GetReport(reportType);
                    this.ReportWriter.Write(report.Item1);
                    this.ReportToCWriter.Write(report.Item2);
                }

                if (!sectionPrinted)
                {
                    this.ReportWriter.WriteLine();
                    this.ReportWriter.WriteLine("There are no <b>" + direction.ToString() + " " + sectionTitle.Replace(" Summary", string.Empty) + "</b> configured.");
                }
            }
            finally
            {
                Logger.Instance.WriteMethodExit("Sync Rule Direction: '{0}'. Sync Rule Report Type: '{1}'.", direction, reportType);
            }
        }

        #endregion Sync Rules

        #region Run Profiles

        /// <summary>
        /// Creates the connector run profile data sets.
        /// </summary>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "No good reason to call Dispose() on DataTable and DataColumn.")]
        protected void CreateConnectorRunProfileDataSets()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var table = new DataTable("ConnectorRunProfiles") { Locale = CultureInfo.InvariantCulture };

                var column1 = new DataColumn("Step Number", typeof(int));
                var column2 = new DataColumn("Step Name");

                table.Columns.Add(column1);
                table.Columns.Add(column2);
                table.PrimaryKey = new[] { column1 };

                var table2 = new DataTable("ConnectorRunProfileConfiguration") { Locale = CultureInfo.InvariantCulture };

                var column12 = new DataColumn("Step Number", typeof(int));
                var column22 = new DataColumn("Setting");
                var column32 = new DataColumn("Configuration");
                var column42 = new DataColumn("Setting Number", typeof(int));

                table2.Columns.Add(column12);
                table2.Columns.Add(column22);
                table2.Columns.Add(column32);
                table2.Columns.Add(column42);
                table2.PrimaryKey = new[] { column12, column22 };

                this.PilotDataSet = new DataSet("ConnectorAnchorConfigurations") { Locale = CultureInfo.InvariantCulture };
                this.PilotDataSet.Tables.Add(table);
                this.PilotDataSet.Tables.Add(table2);

                var dataRelation12 = new DataRelation("DataRelation12", new[] { column1 }, new[] { column12 }, false);

                this.PilotDataSet.Relations.Add(dataRelation12);

                this.ProductionDataSet = this.PilotDataSet.Clone();

                var printTable = this.GetConnectorRunProfilePrintTable();
                this.PilotDataSet.Tables.Add(printTable);
                this.ProductionDataSet.Tables.Add(printTable.Copy());
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Gets the connector run profile print table.
        /// </summary>
        /// <returns>
        /// The connector run profile print table.
        /// </returns>
        protected DataTable GetConnectorRunProfilePrintTable()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                var printTable = Documenter.GetPrintTable();

                // Table 1
                // Step Number
                printTable.Rows.Add((new OrderedDictionary { { "TableIndex", 0 }, { "ColumnIndex", 0 }, { "Hidden", false }, { "SortOrder", 0 }, { "BookmarkIndex", -1 }, { "JumpToBookmarkIndex", -1 }, { "ChangeIgnored", false } }).Values.Cast<object>().ToArray());

                // Step Name
                printTable.Rows.Add((new OrderedDictionary { { "TableIndex", 0 }, { "ColumnIndex", 1 }, { "Hidden", false }, { "SortOrder", -1 }, { "BookmarkIndex", -1 }, { "JumpToBookmarkIndex", -1 }, { "ChangeIgnored", false } }).Values.Cast<object>().ToArray());

                // Table 2
                // Step Number
                printTable.Rows.Add((new OrderedDictionary { { "TableIndex", 1 }, { "ColumnIndex", 0 }, { "Hidden", true }, { "SortOrder", 0 }, { "BookmarkIndex", -1 }, { "JumpToBookmarkIndex", -1 }, { "ChangeIgnored", false } }).Values.Cast<object>().ToArray());

                // Setting
                printTable.Rows.Add((new OrderedDictionary { { "TableIndex", 1 }, { "ColumnIndex", 1 }, { "Hidden", false }, { "SortOrder", -1 }, { "BookmarkIndex", -1 }, { "JumpToBookmarkIndex", -1 }, { "ChangeIgnored", false } }).Values.Cast<object>().ToArray());

                // Configuration
                printTable.Rows.Add((new OrderedDictionary { { "TableIndex", 1 }, { "ColumnIndex", 2 }, { "Hidden", false }, { "SortOrder", -1 }, { "BookmarkIndex", -1 }, { "JumpToBookmarkIndex", -1 }, { "ChangeIgnored", false } }).Values.Cast<object>().ToArray());

                // Setting Number
                printTable.Rows.Add((new OrderedDictionary { { "TableIndex", 1 }, { "ColumnIndex", 3 }, { "Hidden", true }, { "SortOrder", 1 }, { "BookmarkIndex", -1 }, { "JumpToBookmarkIndex", -1 }, { "ChangeIgnored", false } }).Values.Cast<object>().ToArray());

                printTable.AcceptChanges();

                return printTable;
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Creates the connector run profile diffgram.
        /// </summary>
        protected void CreateConnectorRunProfileDiffgram()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                this.DiffgramDataSet = Documenter.GetDiffgram(this.PilotDataSet, this.ProductionDataSet);

                // Setup data relations
                var column1 = this.DiffgramDataSet.Tables[0].Columns["Step Number"];

                var column12 = this.DiffgramDataSet.Tables[1].Columns["Step Number"];

                var dataRelation12 = new DataRelation("DataRelation12", new[] { column1 }, new[] { column12 }, false);

                this.DiffgramDataSet.Relations.Add(dataRelation12);
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        /// <summary>
        /// Prints the extensible2 run profile.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1123:DoNotPlaceRegionsWithinElements", Justification = "Reviewed.")]
        protected void PrintConnectorRunProfile()
        {
            Logger.Instance.WriteMethodEntry();

            try
            {
                #region table

                this.ReportWriter.WriteBeginTag("table");
                this.ReportWriter.WriteAttribute("class", "outer-table");
                this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                {
                    #region thead

                    this.ReportWriter.WriteBeginTag("thead");
                    this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                    {
                        this.ReportWriter.WriteBeginTag("tr");
                        this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                        {
                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Step#");
                            this.ReportWriter.WriteEndTag("th");

                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Step Name");
                            this.ReportWriter.WriteEndTag("th");

                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Setting");
                            this.ReportWriter.WriteEndTag("th");

                            this.ReportWriter.WriteBeginTag("th");
                            this.ReportWriter.WriteAttribute("class", "column-th");
                            this.ReportWriter.Write(HtmlTextWriter.TagRightChar);
                            this.ReportWriter.Write("Configuration");
                            this.ReportWriter.WriteEndTag("th");
                        }

                        this.ReportWriter.WriteEndTag("tr");
                        this.ReportWriter.WriteLine();
                    }

                    this.ReportWriter.WriteEndTag("thead");

                    #endregion thead
                }

                #region rows

                this.WriteRows(this.DiffgramDataSet.Tables[0].Rows.Cast<DataRow>().ToArray(), 0, 0);

                #endregion rows

                this.ReportWriter.WriteEndTag("table");

                #endregion table
            }
            finally
            {
                Logger.Instance.WriteMethodExit();
            }
        }

        #endregion Run Profiles
    }
}