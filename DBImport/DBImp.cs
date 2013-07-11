using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

namespace DBImport
{
    class DBImp
    {
        static void importColumns(string strDestinationTableName, string strDataFilePath, string strHeaderFilePath,
                                       string strFieldExtractorName)
        {
            FileGridProvider gfp = new FileGridProvider(strHeaderFilePath, strDataFilePath, strFieldExtractorName);
            string[][] fields = gfp.getGrid();
            string[] dbColumns = gfp.getHeader();

            TableCreator tc =
                new TableCreator(gfp.getGrid());

            tc.setColumnNames(dbColumns);
            Dictionary<string, string> dict1 = dbColumns.ToDictionary(k => k);
            Hashtable mapping = new Hashtable(dict1);

            SQLServerDataImporter dataImporter = new SQLServerDataImporter(tc, strDestinationTableName, mapping);
            dataImporter.import();
        }

        static void importAndTransformColumns(string strDestinationTableName, string strDataFilePath, string strHeaderFilePath,
                                       string strFieldExtractorName, Hashtable transformToTable, Hashtable transformMethodTable)
        {
            FileGridProvider gfp = new FileGridProvider(strHeaderFilePath, strDataFilePath, strFieldExtractorName);
            string[][] fields = gfp.getGrid();
            string[] dbColumns = gfp.getHeader();

            CompactifiedGridProvider cgp =
                new CompactifiedGridProvider(dbColumns, fields, transformToTable, transformMethodTable);

            TableCreator tc =
                new TableCreator(cgp.getGrid());
            string[] dbColumnsExpanded = cgp.getHeader();
            tc.setColumnNames(dbColumnsExpanded);
            Dictionary<string, string> dict1 = dbColumnsExpanded.ToDictionary(k => k);
            Hashtable mapping = new Hashtable(dict1);

            SQLServerDataImporter dataImporter = new SQLServerDataImporter(tc, strDestinationTableName, mapping);
            dataImporter.import();
        }

        static void Main(string[] args)
        {
            NameValueCollection appSettings;
            try
            {
                appSettings = ConfigurationManager.AppSettings;
            }
            catch (ConfigurationErrorsException e)
            {
                Console.WriteLine("Could not read AppSettings: {0}", e.ToString());
                return;
            }

            if (appSettings.Count == 0)
            {
                Console.WriteLine("AppSettings is empty. Please see the documentation for what mandatory settings it should contain.");
                return;
            }

            string strDestinationTableName = appSettings["DestinationTableName"];
            if (string.IsNullOrEmpty(strDestinationTableName))
                throw (new ApplicationException("DestinationTableName parameter in AppSettings is required."));

            string strDataFilePath = args[0];
            string strHeaderFilePath = appSettings["HeaderFilePath"];
            if (string.IsNullOrEmpty(strHeaderFilePath))
                throw (new ApplicationException("HeaderFilePath parameter in AppSettings is required."));

            string strFieldExtractorName = appSettings["field_extractor_method"];
            if (string.IsNullOrEmpty(strFieldExtractorName))
                strFieldExtractorName = "PluginManager.extractQuoteEnclosedDoubleQuoteEscapedFields";

            string[] keys = appSettings.AllKeys;
            string[] transformToKeys = Array.FindAll(keys, s => s.Contains("transform_to."));
            if (transformToKeys.Length > 0)
            {
                Hashtable transformToTable;
                Hashtable transformMethodTable;
                Dictionary<string, string> dict1 =
                    transformToKeys.ToDictionary(k => k.Substring(k.IndexOf(".") + 1), k => appSettings[k]);
                transformToTable = new Hashtable(dict1);
                // fieldNamesToTransform are the same as transformToKeys, only without the prefixes "transform_to."
                string[] fieldNamesToTransform = dict1.Keys.ToArray();
                string[] transformMethodKeys = Array.FindAll(keys, s => s.Contains("transform_method."));
                Hashtable nameToMethodTable = PluginManager.getMapNameToMethod();
                transformMethodTable = new Hashtable(transformMethodKeys.Length);
                foreach (string transformMethodKey in transformMethodKeys)
                {
                    int ind = transformMethodKey.IndexOf(".");
                    string[] methodNames = appSettings[transformMethodKey].Split(',');
                    if (methodNames.Length > 1)
                    {
                        DelStringTransformer allMethodsDelegate = delegate(string s) { return s; };
                        foreach (string methodName in methodNames)
                            allMethodsDelegate += (DelStringTransformer)nameToMethodTable[methodName];
                        transformMethodTable[transformMethodKey.Substring(ind + 1)] = allMethodsDelegate;
                    }
                    else
                        transformMethodTable[transformMethodKey.Substring(ind + 1)] = nameToMethodTable[appSettings[transformMethodKey]];
                }
                if (fieldNamesToTransform.Length > transformMethodKeys.Length)
                    foreach (string fieldNameToTransform in fieldNamesToTransform)
                        if (!transformMethodKeys.Contains(fieldNameToTransform))
                            transformMethodTable[fieldNameToTransform] = PluginManager.getDefaultTransformMethod(fieldNameToTransform);
                // We ignore the opposite case -- if transformMethodKeys.Length > fieldNamesToTransform.Length
                // because if the user didn't specify what field to convert to what other field,
                // only specified the conversion method, that doesn't make sense.

                importAndTransformColumns(strDestinationTableName, strDataFilePath, strHeaderFilePath, strFieldExtractorName,
                    transformToTable, transformMethodTable);
            } // end if (transformToKeys.Length > 0)
            else
            importColumns(strDestinationTableName, strDataFilePath, strHeaderFilePath, strFieldExtractorName);
        }
    }
}
