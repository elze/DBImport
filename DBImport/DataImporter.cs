using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace DBImport
{
    public interface IDataImporter
    {
        void import();
    }

    public class SQLServerDataImporter : IDataImporter
    {
        private ITableCreator tableCreator;
        private string destinationTableName;
        private Hashtable sourceDestColumnMap;

        public SQLServerDataImporter(ITableCreator tc, string strDestinationTableName, Hashtable sourceDestColumnMapping)
        {
            tableCreator = tc;
            destinationTableName = strDestinationTableName;
            sourceDestColumnMap = sourceDestColumnMapping;
        }

        public virtual void import()
        {
            DataTable dataTable = tableCreator.getDataTable();

            string strConnection =
                ConfigurationManager.ConnectionStrings["DBImport.Properties.Settings.PageVisitsConnectionString"].
                    ConnectionString;
            using (SqlConnection connection = new SqlConnection(strConnection))
            {
                connection.Open();
                SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(connection);
                sqlBulkCopy.DestinationTableName = destinationTableName;

                foreach (string sourceColName in sourceDestColumnMap.Keys)
                {
                    SqlBulkCopyColumnMapping sqlBCCMapping = new SqlBulkCopyColumnMapping(sourceColName, (string)sourceDestColumnMap[sourceColName]);
                    sqlBulkCopy.ColumnMappings.Add(sqlBCCMapping);
                }
                try
                {
                    // Write from the source to the destination.
                    sqlBulkCopy.WriteToServer(dataTable);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            } // end using
        } // end import()
    } // end class SQLServerDataImporter
}
