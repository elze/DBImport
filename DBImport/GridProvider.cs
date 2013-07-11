using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

namespace DBImport
{
    public delegate string DelStringTransformer(string message);

    public delegate string[][] DelFieldExtractor(string[] lines);

    public interface IHeaderProvider
    {
        string[] getHeader();
    }

    public interface IGridProvider
    {        
        string[][] getGrid();
        string[] getHeader();
    }

    public class FileGridProvider : IGridProvider
    {
        protected string headerFilePath;
        protected string[] columnNames;
        protected string textFilePath;
        protected string[][] fields;
        protected string[] allLines;
        protected string fieldExtractorMethod;

        protected Hashtable fieldExtractMethodTable;

        public FileGridProvider(string strHeaderFilePath, string strTextFilePath, string fExtractorMethod)
        {
            headerFilePath = strHeaderFilePath;
            textFilePath = strTextFilePath;
            fieldExtractorMethod = fExtractorMethod;
        }

        protected virtual void getLinesForParsing()
        {
            allLines = File.ReadAllLines(textFilePath);
        }

        public virtual string[][] getGrid()
        {
            if (fields == null)
            {
                populateGrid();
            }
            return fields;
        }

        public virtual string[] getHeader()
        {
            if (columnNames == null)
            {
                populateHeader();
            }
            return columnNames;
        }

        protected virtual void populateHeader()
        {
            String header = "";
            try
            {
                using (StreamReader sr = new StreamReader(headerFilePath))
                {
                    header = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The file {0} could not be read:", headerFilePath);
                Console.WriteLine(e.Message);
            }

            header = header.Replace("\"", "");
            columnNames = header.Split(',');
        }

        protected virtual void populateGrid()
        {
            getLinesForParsing();

            Hashtable nameToMethodTable = PluginManager.getMapNameToMethod();
            DelFieldExtractor fieldExtractor = (DelFieldExtractor)nameToMethodTable[fieldExtractorMethod];

            fields = fieldExtractor(allLines);            
        }
    }

    public class CompactifiedGridProvider : IGridProvider
    {
        protected string[][] fields;
        protected string[] columnNames;
        protected int[] indicesOfColumnsToCompactify;
        protected int[] indicesOfCompactifiedColumns;
        protected Hashtable columnsToCompactifyNameToIndex;
        protected Hashtable compactifiedColumnsNameToIndex;        
        protected Hashtable compactificationColumnMap;
        protected Hashtable transformMethodTable;

        public CompactifiedGridProvider(string[] colNames, string[][] f, Hashtable compColumnMap, 
                                        Hashtable transfMethodTable)
        {
            columnNames = colNames;
            fields = f;
            compactificationColumnMap = compColumnMap;
            transformMethodTable = transfMethodTable;
            columnsToCompactifyNameToIndex = new Hashtable(compactificationColumnMap.Count);
            compactifiedColumnsNameToIndex = new Hashtable(compactificationColumnMap.Count);
        }

        public virtual string[] getHeader()
        {
            if (indicesOfCompactifiedColumns.Length == 0)
            {
                populateHeader();
            }
            return columnNames;
        }

        protected virtual void populateHeader()
        {
            indicesOfColumnsToCompactify = new int[compactificationColumnMap.Count];
            indicesOfCompactifiedColumns = new int[compactificationColumnMap.Count];
            
            int columnNamesLength = columnNames.Length;
            int i = 0;

            foreach (string key in compactificationColumnMap.Keys)
            {
                int indOfFieldToCompactify = Array.IndexOf(columnNames, key);
                indicesOfColumnsToCompactify[i] = indOfFieldToCompactify;
                columnsToCompactifyNameToIndex[key] = indOfFieldToCompactify;

                int indexOfCompactifiedColumn = Array.IndexOf(columnNames, compactificationColumnMap[key]);
                if (indexOfCompactifiedColumn == -1)
                {
                    // This means that the header row (columnNames) does not contain 
                    // compactified column names.
                    // The compactified columns are at the end of the table 
                    // in the alphabetical order. 
                    columnNamesLength++;
                    int indOfCompactifiedColumn = columnNamesLength - 1;
                    indicesOfCompactifiedColumns[i] = indOfCompactifiedColumn;
                    Array.Resize(ref columnNames, columnNamesLength);
                    columnNames[indOfCompactifiedColumn] = (string)compactificationColumnMap[key];
                    compactifiedColumnsNameToIndex[key] = indOfCompactifiedColumn;
                }
                i++;
            }

        }


        public virtual string[][] getGrid()
        {
            if (indicesOfCompactifiedColumns == null)
            {
                populateHeader();
            }

            if (fields[0].Length < columnNames.Length)
            {
                populateGrid();
            }
            return fields;
        }

        protected virtual void populateGrid()
        {
            for (int rowInd = 0; rowInd < fields.Length; rowInd++)
            {
                foreach (string key in compactificationColumnMap.Keys)
                {
                    DelStringTransformer compactifier = (DelStringTransformer)transformMethodTable[key];
                    Delegate[] methods = compactifier.GetInvocationList();
                    string retValue1 = fields[rowInd][(int)columnsToCompactifyNameToIndex[key]];
                    foreach (DelStringTransformer m in methods)
                    {
                        string retValue2 = m(retValue1);
                        retValue1 = retValue2;
                    }
                    string compactified = retValue1;
                    // tableRow should always be shorter than columnNames, because
                    // columnNames also includes those of compactified columns,
                    // whereas tableRow would not include those columns.
                    Array.Resize(ref fields[rowInd], columnNames.Length);
                    fields[rowInd][(int)compactifiedColumnsNameToIndex[key]] = compactified;
                }
            }
        }
    }
}
