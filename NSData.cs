using System;
using System.Net;
using NetSuiteAccess.com.netsuite.webservices;
using System.Xml;
using System.Data;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace NetSuiteAccess
{
    
    public class NSData
    {
        /// <summary> Proxy class that abstracts the communication with the NetSuite Web
        /// Services. All NetSuite operations are invoked as methods of this class.
        /// </summary>
        private NetSuiteService _service;

        /// <value>A NameValueCollection that abstracts name/value pairs from
        /// the app.config file in the Visual .NET project. This file is called
        /// [AssemblyName].exe.config in the distribution</value>
        //private System.Collections.Specialized.NameValueCollection _dataCollection;


        /// <summary> Requested page size for search
        /// </summary>
        private int _pageSize;

        /// Set up request level preferences as a SOAP header
        private Preferences _prefs;
        private SearchPreferences _searchPreferences;

        private String PENDING_APPROVAL_STRING = "_salesOrderPendingApproval";

        private String PENDING_FULFILLMENT_STRING = "_salesOrderPendingFulfillment";

        private String PRICE_CURRENCY_INTERNAL_ID = "1";

        private String BASE_PRICE_LEVEL_INTERNAL_ID = "1";

        private String ITEM_TYPE = "_item";

        private String TRANSACTION_TYPE = "_salesOrder";

        private String ASSEMBLY_TRANSACTION_TYPE = "_assemblyBuild";

        		/// <summary>
		/// Summary description for NSClient.
		/// </summary>
		public NSData()
		{			
            try
            {
			// Setting pageSize to 5 records per page
			_pageSize = 1000;
					
			// Reference to config file that contains sample data. This file
			// is named App.config in the Visual .NET project or, <AssemblyName>.exe.config
			// in the distribution
			//_dataCollection = System.Configuration.ConfigurationManager.AppSettings;

			//Decide between standard login and TBA
			//useTba = "true".Equals(_dataCollection["login.useTba"]);

            // Instantiate the NetSuite web services
			_service = new DataCenterAwareNetSuiteService("4538736");  // the netsuite account for ABS
            _service.Timeout = 1000 * 60 * 60 * 2;

            // Force TLS 1.2
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // In order to enable SOAPscope to work through SSL. Refer to FAQ for more details
            ServicePointManager.ServerCertificateValidationCallback += delegate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
            {
                return true;
            };

            setPreferences();

            }
            catch (System.Web.Services.Protocols.SoapException ex)
            {
                // Get the fault type. It's the only child element of the detail element.
                String fault = ex.Detail.FirstChild.Name;

                // Get the list of child elements of the fault type element. 
                // It should include the code and message elements
                System.Collections.IEnumerator ienum = ex.Detail.FirstChild.ChildNodes.GetEnumerator();
                String code = null;
                String message = null;
                while (ienum.MoveNext())
                {
                    XmlNode node = (XmlNode)ienum.Current;
                    if (node.Name.Equals("code"))
                        code = node.InnerText;
                    else if (node.Name.Equals("message"))
                        message = node.InnerText;
                }
               
            }
            catch (System.Net.WebException ex)
            {
               
            }
            catch (System.InvalidOperationException ex)
            {
                
            }
            catch (System.Exception ex)
            {
                
            }
            finally
            {
            }

		}


        /// <summary>
        /// <p>Demonstrates how to use the request level login on each operation with attached passport object.</p>
        /// </summary>
        /// <remarks>The following field values used for the login are obtained 
        /// from the config file or the user is prompted:
        /// <list type="bullet">
        /// <item>
        /// <description>email</description>
        /// </item>
        /// <item>
        /// <description>password</description>
        /// </item>
        /// <item>
        /// <description>role</description>
        /// </item>
        /// <item>
        /// <description>account</description>
        /// </item>
        /// </list>
        /// </remarks>
        private void prepareLoginPassport()
        {

                if (_service.passport == null)
                {
                    _service.applicationInfo = createApplicationId();

                    // Populate Passport object with all login information
                    Passport passport = new Passport();
                    RecordRef role = new RecordRef();

                        passport.email = "garland.p@gmail.com";
                        passport.password = "3201Saratoga";
                        role.internalId = "3";
                        passport.role = role;
                        passport.account = "4538736";

                    _service.passport = passport;
                }

        }



        /// <summary> Searches for InventoryItem records based on a keyword entered by
        /// the user.  All inventory items, including parent items,  with the 
        /// keyword found in their name field will be returned.	
        /// </summary>
        public DataTable getItems()
        {
            DataTable outTable = null;
            ItemSearch itemSearch = new ItemSearch();

            SearchEnumMultiSelectField type = new SearchEnumMultiSelectField();

            type.@operator = SearchEnumMultiSelectFieldOperator.anyOf;
            type.operatorSpecified = true;
            type.searchValue = new String[] { "_assembly" }; // "_inventoryItem", "_kitItem", "_serializedAssemblyItem", "_serializedInventoryItem", "_otherNameCategory", "_otherChargeSaleItem", "_assemblyItem", "_otherItem", "_lotNumberedAssemblyItem", "_lotNumberedInventoryItem" }; //inv;
            //RecordType.o
            SearchBooleanField isLotItem = new SearchBooleanField();
            isLotItem.searchValue = true;
            isLotItem.searchValueSpecified = true;

            SearchBooleanField isSerialItem = new SearchBooleanField();
            isSerialItem.searchValue = true;
            isSerialItem.searchValueSpecified = false;

            ItemSearchBasic itemBasic = new ItemSearchBasic();
            itemBasic.type = type;
            itemSearch.basic = itemBasic;

            SearchResult res = _service.search(itemSearch);
            if (res.status.isSuccess)
            {

                Record[] recordList;
                outTable = new DataTable();
                outTable.Clear();
                outTable.Columns.Add("ItemID");
                outTable.Columns.Add("ItemNo");
                outTable.Columns.Add("ItemDescription");
                outTable.Columns.Add("TARGET_WEIGHT");
                outTable.Columns.Add("ROLLS_PER_PALLET");
                outTable.Columns.Add("IsHandroll");
                outTable.Columns.Add("CustomerItemCode");
                outTable.Columns.Add("CustomerDescription");

                //_out.writeLn("\nNumber of items found with the keyword " + keyWordString + ": " + res.totalRecords);
                if (res.totalRecords > 0)
                {
                    //_out.writeLn("\nPage size: " + res.pageSize);

                    for (int i = 1; i <= res.totalPages; i++)
                    {
                        recordList = res.recordList;

                        for (int j = 0; j < recordList.Length; j++)
                        {
                            if (recordList[j] is LotNumberedAssemblyItem)
                            {
                                LotNumberedAssemblyItem item = (LotNumberedAssemblyItem)(recordList[j]);
                                DataRow outRow = LoadItemFields(outTable, item);
                                outTable.Rows.Add(outRow);
                            }
                        }
                        if (res.pageIndex < res.totalPages)
                        {
                            res = _service.searchMoreWithId(res.searchId, i + 1);
                        }
                    }
                }
            }
            else
            {

            }

            return outTable;
        }


        /// <summary> Searches for InventoryItem records based on a keyword entered by
        /// the user.  All inventory items, including parent items,  with the 
        /// keyword found in their name field will be returned.	
        /// </summary>
        public DataTable getItem(String itemName)
        {
            DataTable outTable = null;
            ItemSearch itemSearch = new ItemSearch();

            SearchEnumMultiSelectField type = new SearchEnumMultiSelectField();

            type.@operator = SearchEnumMultiSelectFieldOperator.anyOf;
            type.operatorSpecified = true;
            type.searchValue = new String[] { "_assembly" }; // "_inventoryItem", "_kitItem", "_serializedAssemblyItem", "_serializedInventoryItem", "_otherNameCategory", "_otherChargeSaleItem", "_assemblyItem", "_otherItem", "_lotNumberedAssemblyItem", "_lotNumberedInventoryItem" }; //inv;
            //RecordType.o
            SearchBooleanField isLotItem = new SearchBooleanField();
            isLotItem.searchValue = true;
            isLotItem.searchValueSpecified = true;

            SearchBooleanField isSerialItem = new SearchBooleanField();
            isSerialItem.searchValue = true;
            isSerialItem.searchValueSpecified = false;

            ItemSearchBasic itemBasic = new ItemSearchBasic();
            itemBasic.type = type;
            itemSearch.basic = itemBasic;

            SearchResult res = _service.search(itemSearch);
            if (res.status.isSuccess)
            {

                Record[] recordList;
                outTable = new DataTable();
                outTable.Clear();
                outTable.Columns.Add("ItemID");
                outTable.Columns.Add("ItemNo");
                outTable.Columns.Add("ItemDescription");
                outTable.Columns.Add("TARGET_WEIGHT");
                outTable.Columns.Add("ROLLS_PER_PALLET");
                outTable.Columns.Add("IsHandroll");
                outTable.Columns.Add("CustomerItemCode");
                outTable.Columns.Add("CustomerDescription");

                //_out.writeLn("\nNumber of items found with the keyword " + keyWordString + ": " + res.totalRecords);
                if (res.totalRecords > 0)
                {
                    //_out.writeLn("\nPage size: " + res.pageSize);

                    for (int i = 1; i <= res.totalPages; i++)
                    {
                        recordList = res.recordList;

                        for (int j = 0; j < recordList.Length; j++)
                        {
                            if (recordList[j] is LotNumberedAssemblyItem)
                            {
                                LotNumberedAssemblyItem item = (LotNumberedAssemblyItem)(recordList[j]);

                                if (item.itemId.Equals(itemName))
                                {
                                    DataRow outRow = LoadItemFields(outTable, item);
                                    outTable.Rows.Add(outRow);
                                }
                            }
                        }
                        if (res.pageIndex < res.totalPages)
                        {
                            res = _service.searchMoreWithId(res.searchId, i + 1);
                        }
                    }
                }
            }
            else
            {

            }

            return outTable;
        }


        public DataTable getWorkOrders()
        {
            DataTable outTable = null;

            try
            {
                TransactionSearch woSearch = new TransactionSearch();
                SearchStringField woNum = new SearchStringField();
                woNum.@operator = SearchStringFieldOperator.contains;
                woNum.operatorSpecified = true;
                woNum.searchValue = "WO";

                TransactionSearchBasic tsBasic = new TransactionSearchBasic();
                tsBasic.tranId = woNum;

                woSearch.basic = tsBasic;

                SearchResult res = _service.search(woSearch);

                if (res.status.isSuccess)
                {
                    Record[] recordList;

                    outTable = new DataTable();
                    outTable.Clear();
                    outTable.Columns.Add("OrderID");
                    outTable.Columns.Add("MfgOrderNo");
                    outTable.Columns.Add("ItemID");
                    outTable.Columns.Add("QtyOrdered");
                    outTable.Columns.Add("QtyCompleted");
                    outTable.Columns.Add("CustomerID");
                    outTable.Columns.Add("CreatedFrom");


                    for (int i = 1; i <= res.totalPages; i++)
                    {
                        recordList = res.recordList;

                        for (int j = 0; j < recordList.Length; j++)
                        {
                            if (recordList[j] is WorkOrder)
                            {
                                WorkOrder workorder = (WorkOrder)(recordList[j]);
                                DataRow outRow = LoadWOFields(outTable, workorder);
                                outTable.Rows.Add(outRow);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }

            return outTable;
        }


        public DataTable getWorkOrder(String worder)
        {
            DataTable outTable = null;

            try
            {
                TransactionSearch woSearch = new TransactionSearch();
                SearchStringField woNum = new SearchStringField();
                woNum.@operator = SearchStringFieldOperator.@is;
                woNum.operatorSpecified = true;
                woNum.searchValue = worder;

                TransactionSearchBasic tsBasic = new TransactionSearchBasic();
                tsBasic.tranId = woNum;

                woSearch.basic = tsBasic;

                SearchResult res = _service.search(woSearch);

                if (res.status.isSuccess)
                {
                    Record[] recordList;

                    outTable = new DataTable();
                    outTable.Clear();
                    outTable.Columns.Add("OrderID");
                    outTable.Columns.Add("MfgOrderNo");
                    outTable.Columns.Add("ItemID");
                    outTable.Columns.Add("ItemNo");
                    outTable.Columns.Add("QtyOrdered");
                    outTable.Columns.Add("QtyCompleted");
                    outTable.Columns.Add("CustomerID");
                    outTable.Columns.Add("CreatedFrom");


                    for (int i = 1; i <= res.totalPages; i++)
                    {
                        recordList = res.recordList;

                        for (int j = 0; j < recordList.Length; j++)
                        {
                            if (recordList[j] is WorkOrder)
                            {
                                WorkOrder workorder = (WorkOrder)(recordList[j]);
                                DataRow outRow = LoadWOFields(outTable, workorder);
                                outTable.Rows.Add(outRow);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }

            return outTable;
        }



        private DataRow LoadItemFields(DataTable schema, LotNumberedAssemblyItem item)
        {
                /*
                outTable.Columns.Add("Name");
                outTable.Columns.Add("Marks");
                DataRow _dr = outTable.NewRow();
                _dr["Name"] = "ravi";
                _dr["Marks"] = "500";
                outTable.Rows.Add(_dr);
                */

            DataRow dr = null;

            try
            {

                if (item != null)
                {
                    dr = schema.NewRow();

                    if (item.description != null)
                    {
                        dr["ItemDescription"] = item.description;
                    }

                    if (item.itemId != null)
                    {
                        dr["ItemNo"] = item.itemId;
                        //if (item.itemId.Contains("ARM"))
                        //{
                        //    dr["IsHandroll"] = 1;
                        //}
                        //else
                        //{
                        //    dr["IsHandroll"] = 0;
                        //}
                    }

                    if (item.upcCode != null && item.upcCode.Length > 0)
                    {
                        dr["upcCode"] = item.upcCode;
                    }

                    if (item.displayName != null && item.displayName.Length > 0)
                    {
                        dr["displayName"] = item.displayName;
                    }

                    if (item.internalId != null)
                    {
                        dr["ItemID"] = item.internalId;
                    }

                    for (int i = 0; i < item.customFieldList.Length; i++)
                    {
                        switch (item.customFieldList[i].scriptId)
                        {
                            case "custitem_oasis_rollnetweight":

                                DoubleCustomFieldRef cfr = (DoubleCustomFieldRef)item.customFieldList[i];
                                dr["TARGET_WEIGHT"] = cfr.value;

                                break;

                            case "custitem_oasis_rollsperpallet":

                                LongCustomFieldRef cff = (LongCustomFieldRef)item.customFieldList[i];
                                dr["ROLLS_PER_PALLET"] = cff.value;

                                break;

                            case "custitem2":

                                SelectCustomFieldRef scfr = (SelectCustomFieldRef)item.customFieldList[i];
                                if (scfr.value.name.ToLower().Equals("armor") ||
                                    scfr.value.name.ToLower().Equals("shield") ||
                                    scfr.value.name.ToLower().Equals("ultra hand film"))
                                    dr["IsHandroll"] = 1;
                                else
                                    dr["IsHandroll"] = 0;
                                break;

                            case "custitem_customeritemcode":
                                StringCustomFieldRef cic = (StringCustomFieldRef)item.customFieldList[i];
                                dr["CustomerItemCode"] = cic.value;
                                break;

                            case "custitem_customerdescription":
                                StringCustomFieldRef cdesc = (StringCustomFieldRef)item.customFieldList[i];
                                dr["CustomerDescription"] = cdesc.value;
                                break;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }

            return dr;
        }


        private DataRow LoadWOFields(DataTable schema, WorkOrder wo)
        {
            DataRow dr = null;

            try
            {

                if (wo != null)
                {
                    dr = schema.NewRow();

                    if (wo.internalId != null)
                    {
                        dr["OrderID"] = wo.internalId;
                    }

                    if (wo.tranId != null)
                    {
                        dr["MfgOrderNo"] = wo.tranId;
                    }

                    if (wo.assemblyItem != null)
                    {
                        dr["ItemID"] = wo.assemblyItem.internalId;
                    }

                    if (wo.assemblyItem != null)
                    {
                        dr["ItemNo"] = wo.assemblyItem.name;
                    }

                    if (wo.createdFrom != null)
                    {
                        dr["CreatedFrom"] = wo.createdFrom.name.Substring(wo.createdFrom.name.IndexOf("#") + 1).Trim();
                    }
                    
                    if (wo.quantitySpecified)
                    {
                        dr["QtyOrdered"] = wo.quantity;
                    }

                    //if (wo.built != null)
                    //{
                    dr["QtyCompleted"] = (int)wo.built;
                    //}

                    if (wo.entity != null)
                        dr["CustomerID"] = wo.entity.name.Trim();
                    else
                        dr["CustomerID"] = "";

                }
            }
            catch (Exception ex)
            {
                throw;
            }

            return dr;
        }

        /// <summary>
        /// <p>This function builds the Pereferences and SearchPreferences in the SOAP header. </p>
        /// </summary>
        private void setPreferences()
        {
            // Set up request level preferences as a SOAP header
            _prefs = new Preferences();
            _service.preferences = _prefs;
            _searchPreferences = new SearchPreferences();
            _service.searchPreferences = _searchPreferences;

            // Preference to ask NS to treat all warnings as errors
            _prefs.warningAsErrorSpecified = true;
            _prefs.warningAsError = false;
            _searchPreferences.pageSize = _pageSize;
            _searchPreferences.pageSizeSpecified = true;
            // Setting this bodyFieldsOnly to true for faster search times on Opportunities
            _searchPreferences.bodyFieldsOnly = false;
            prepareLoginPassport();
        }

        /// <summary>
        /// Update search preference to either return body fields, return columns or full records
        /// </summary>
        /// <param name="bodyFieldsOnly"></param>
        /// <param name="returnColumns"></param>
        private void setSearchPreferences(bool bodyFieldsOnly, bool returnColumns)
        {
            _service.searchPreferences.bodyFieldsOnly = bodyFieldsOnly;
            _service.searchPreferences.returnSearchColumns = returnColumns;
        }


        /// <summary> 
        /// Processes the status object and prints the status details		
        /// </summary>	
        private System.String getStatusDetails(Status status)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < status.statusDetail.Length; i++)
            {
                sb.Append("[Code=" + status.statusDetail[i].code + "] " + status.statusDetail[i].message + "\n");
            }
            return sb.ToString();
        }

        private TokenPassport createTokenPassport()
        {
            string account = "4538736";
            string consumerKey = "CONSUMERKEY";
            string consumerSecret = "CONSUMERSECRET";
            string tokenId = "TOKENID";
            string tokenSecret = "TOKENSECRET";

            string nonce = computeNonce();
            long timestamp = computeTimestamp();
            TokenPassportSignature signature = computeSignature(account, consumerKey, consumerSecret, tokenId, tokenSecret, nonce, timestamp);

            TokenPassport tokenPassport = new TokenPassport();
            tokenPassport.account = account;
            tokenPassport.consumerKey = consumerKey;
            tokenPassport.token = tokenId;
            tokenPassport.nonce = nonce;
            tokenPassport.timestamp = timestamp;
            tokenPassport.signature = signature;
            return tokenPassport;
        }

        private string computeNonce()
        {
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] data = new byte[20];
            rng.GetBytes(data);
            int value = Math.Abs(BitConverter.ToInt32(data, 0));
            return value.ToString();
        }

        private long computeTimestamp()
        {
            return ((long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
        }

        private TokenPassportSignature computeSignature(string compId, string consumerKey, string consumerSecret,
                                        string tokenId, string tokenSecret, string nonce, long timestamp)
        {
            string baseString = compId + "&" + consumerKey + "&" + tokenId + "&" + nonce + "&" + timestamp;
            string key = consumerSecret + "&" + tokenSecret;
            string signature = "";
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyBytes = encoding.GetBytes(key);
            byte[] baseStringBytes = encoding.GetBytes(baseString);
            using (var hmacSha1 = new HMACSHA1(keyBytes))
            {
                byte[] hashBaseString = hmacSha1.ComputeHash(baseStringBytes);
                signature = Convert.ToBase64String(hashBaseString);
            }
            TokenPassportSignature sign = new TokenPassportSignature();
            sign.algorithm = "HMAC-SHA1";
            sign.Value = signature;
            return sign;
        }

        private ApplicationInfo createApplicationId()
        {
            ApplicationInfo applicationInfo = new ApplicationInfo();
            applicationInfo.applicationId = "8CA80836-4422-4A14-B91D-B386AE9815FD";
            return applicationInfo;
        }
    }

    class OverrideCertificatePolicy : ICertificatePolicy
    {
        public bool CheckValidationResult(ServicePoint srvPoint, System.Security.Cryptography.X509Certificates.X509Certificate certificate, WebRequest request, int certificateProblem)
        {
            return true;
        }
    }

    /// <summary>    
    /// Since 12.2 endpoint accounts are located in multiple datacenters with different domain names.
    /// In order to use the correct one, wrap the Service and get the correct domain first.
    ///
    /// See getDataCenterUrls WSDL method documentation in the Help Center.	 
    /// </summary>
    class DataCenterAwareNetSuiteService : NetSuiteService
    {
        public DataCenterAwareNetSuiteService(string account)
            : base()
        {
            System.Uri originalUri = new System.Uri(this.Url);
            DataCenterUrls urls = getDataCenterUrls(account).dataCenterUrls;
            Uri dataCenterUri = new Uri(urls.webservicesDomain + originalUri.PathAndQuery);
            this.Url = dataCenterUri.ToString();
        }
    }


}
