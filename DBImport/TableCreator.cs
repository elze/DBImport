using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace DBImport
{
    public interface ITableCreator
    {
        DataTable getDataTable();
    }

    public class TableCreator : ITableCreator
    {
        protected string[] columnNames;
        protected string[][] fields;
        public TableCreator(string[][] dataFields)
        {
            fields = dataFields;
        }

        public virtual void setColumnNames(string[] colNames)
        {
            columnNames = colNames;
        }

        public virtual DataTable getDataTable()
        {
            DataTable dt = new DataTable();
            foreach (string columnName in columnNames)
            {
                dt.Columns.Add(columnName);
            }

            DataRow row; //Declare a row, which will be added to the above data table
            foreach (string[] tableRow in fields)
            {
                row = dt.NewRow();
                for (int i = 0; i < tableRow.Length; i++)
                {
                    row[columnNames[i]] = tableRow[i];
                }
                dt.Rows.Add(row);
            }
            return dt;
        }
    }
}
