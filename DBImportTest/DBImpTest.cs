using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

using DBImport;

namespace DBImportTest
{
    using NUnit.Framework;

    [TestFixture]
    public class DBImpTest
    {
        public void setAppSettings()
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration("c:\\Users\\elze\\Documents\\Visual Studio 2010\\Projects\\DBImport\\DBImport\\bin\\Debug\\DBImport.exe");
            string[] keys = config.AppSettings.Settings.AllKeys;
            string[] searchTermRegexKeys = Array.FindAll(keys, k => k.Contains("search_term_regex"));
            Dictionary<string, string> dict1 = searchTermRegexKeys.ToDictionary(k => k.Substring(k.IndexOf(".") + 1), k => config.AppSettings.Settings[k].Value);
            SortedDictionary<string, string> sDict1 = new SortedDictionary<string, string>(dict1);
            string[] searchTermRegexes = new string[sDict1.Count];
            sDict1.Values.CopyTo(searchTermRegexes, 0);

            string[] imageExtrRegexKeys = Array.FindAll(keys, k => k.Contains("image_extraction_regex"));
            PluginManager.setAppSettings(searchTermRegexKeys, imageExtrRegexKeys);
            Dictionary<string, string> dict2 = imageExtrRegexKeys.ToDictionary(k => k.Substring(k.IndexOf(".") + 1), k => config.AppSettings.Settings[k].Value);
            SortedDictionary<string, string> sDict2 = new SortedDictionary<string, string>(dict2);

            string[] imageExtrRegexes = new string[sDict2.Count];
            sDict2.Values.CopyTo(imageExtrRegexes, 0);
            int compactifierSegmentSize;
            System.Int32.TryParse(config.AppSettings.Settings["CompactifierSegmentSize"].Value, out compactifierSegmentSize);
            PluginManager.setAppSettings(searchTermRegexes, imageExtrRegexes);
            PluginManager.setCompactifierSegmentSize(compactifierSegmentSize);
        }

        [Test]
        public void fileGridProviderGetGridShouldReturnValues()
        {
            FileGridProvider fgp = new FileGridProvider("C:\\Users\\elze\\Documents\\Visual Studio 2010\\Projects\\DBImport\\DBImport\\TestHeader.csv",
                                                        "C:\\Users\\elze\\Documents\\Visual Studio 2010\\Projects\\DBImport\\DBImport\\TestGrid.csv",
                                                        "PluginManager.extractQuoteEnclosedDoubleQuoteEscapedFields");
            string[][] grid = fgp.getGrid();
            string[][] agGrid = { new string[] {"1","all_girl_hack_night","All Girl Hack Night"}, new string[] {"2","austin_software_readers","Austin Software Readers"},
                                  new string[] {"3","drupal_newbies","Drupal Newbies"}, new string[] {"4","girl_develop_it","Girl Develop It"}, new string[] {"5","dotnet_developers",".NET Developers"}};
            int i = 0;
            foreach (string[] tableRow in grid)
                Assert.AreEqual(agGrid[i++], tableRow);
        }

        [Test]
        public void tableCreatorGetDataTableShouldReturnValues()
        {
            FileGridProvider fgp = new FileGridProvider("C:\\Users\\elze\\Documents\\Visual Studio 2010\\Projects\\DBImport\\DBImport\\TestHeader.csv",
                                                        "C:\\Users\\elze\\Documents\\Visual Studio 2010\\Projects\\DBImport\\DBImport\\TestGrid.csv",
                                                        "PluginManager.extractQuoteEnclosedDoubleQuoteEscapedFields");
            TableCreator tc = new TableCreator(fgp.getGrid());
            tc.setColumnNames(fgp.getHeader());
            DataTable dt = tc.getDataTable();
            Console.WriteLine("tableCreatorGetDataTableShoudReturnValues()");
            string[][] agGrid = { new string[] {"1","all_girl_hack_night","All Girl Hack Night"}, new string[] {"2","austin_software_readers","Austin Software Readers"},
                                  new string[] {"3","drupal_newbies","Drupal Newbies"}, new string[] {"4","girl_develop_it","Girl Develop It"}, new string[] {"5","dotnet_developers",".NET Developers"}};

            using (DataTableReader reader = new DataTableReader(dt))
            {
                int i = 0;
                while (reader.Read())
                {
                    string[] values = new string[reader.FieldCount];
                    int fieldCount = reader.GetValues(values);
                    Console.WriteLine("fieldCount = {0}", fieldCount);
                    Assert.AreEqual(agGrid[i++], values);
                }
            }
        }

         
        [Test]
        public void compactifyReferringURLShouldReturnCompactString()
        {
            setAppSettings();

            string strToExtractFromPossiblyEncoded1url = "http://www.google.com/url?sa=t&rct=j&q=&esrc=s&source=web&cd=10&sqi=2&ved=0CGgQFjAJ&url=http%3A%2F%2Fgallery.geekitude.com%2Fv%2F2007%2F2007Cruise%2FmayanRuins%2FCIMG8257BeheadedPlayerBlood.jpg.html&ei=pCYtUIPrEsTS0QWQ14DADA&usg=AFQjCNFnK67IlOAOHjIjIIgLnEDnAIBr3Q&sig2=MytvKIxNnXiGfu9o7sGw_Q";
            string strCompact1url = PluginManager.compactifyReferringURL(strToExtractFromPossiblyEncoded1url);
            //Assert.AreEqual("http://www.google.com/url?sa=t&rct=j&q=&esrc=s&source=web&cd=10&sqi=2&ved=0CGgQFjAJ&url=http://gallery.geekitude.com/v/2007/2007Cruise/mayanRuins/CIMG8257BeheadedPlayerBlood.jpg.html&ei=pCYtUIPrEsTS0QWQ14DADA&usg=AFQjCNFnK67IlOAOHjIjIIgLnEDnAIBr3Q&sig2=MytvKIxNnXiGfu9o7sGw_Q", strCompact); -- no, that's the decoded string
            Assert.AreEqual("... CIMG8257BeheadedPlayerBlood.jpg ...", strCompact1url);
            string strToExtractFromPossiblyEncoded2searchfor = "http://search.mywebsearch.com/mywebsearch/AJimage.jhtml?searchfor=hanging gardens of  babylon image&id=XPxdm045BCin&ts=1345002565748&ptnrS=XPxdm045BCin&n=77ed255d&ss=pop-sub&st=hp&ptb=1DBABCFE-9816-4824-94D0-98EBB5A66C70&tpr=sbt&ps=hanging gardens of  babylon image";
            string strCompact2searchfor = PluginManager.compactifyReferringURL(strToExtractFromPossiblyEncoded2searchfor);
            Assert.AreEqual("... searchfor=hanging gardens of  babylon image ... ps=hanging gardens of  babylon image ...", strCompact2searchfor);
            string strToExtractFromPossiblyEncoded3p = "http://images.search.yahoo.com/images/view;_ylt=A0PDoX3zSC1QC3wAIy2JzbkF;_ylu=X3oDMTBlMTQ4cGxyBHNlYwNzcgRzbGsDaW1n?back=http%3A%2F%2Fimages.search.yahoo.com%2Fsearch%2Fimages%3Fp%3Dkligong%2Bwomen%26n%3D30%26ei%3Dutf-8%26y%3DSearch%26fr%3Dyfp-t-701%26tab%3Dorganic%26ri%3D4&w=800&h=793&imgurl=pic.geekitude.com%2Fd%2F5943-2%2FP1020516KlingonWomen.jpg&rurl=http%3A%2F%2Fpic.geekitude.com%2Fv%2Fsf%2Ffencon2008%2Ffencos2008%2FP1020516KlingonWomen.jpg.html&size=104.1 KB&name=P1020516 Klingon women&p=kligong women&oid=470ea4ed374b03d1e3ae7a18302bdb2b&fr2=&fr=yfp-t-701&rw=klingon women&tt=P1020516%2BKl";
            string strCompact3p = PluginManager.compactifyReferringURL(strToExtractFromPossiblyEncoded3p);
            Assert.AreEqual("... p=kligong women ... p=kligong women ... rw=klingon women ... P1020516KlingonWomen.jpg ...", strCompact3p);
            string strToExtractFromPossiblyEncoded4LinkedIn = "http://www.linkedin.com/profile/view?id=3744112&authType=OUT_OF_NETWORK&authToken=mvkv&locale=en_US&srchid=62bdf9f9-4ace-405a-b91a-f60a358bb11c-0&srchindex=55&srchtotal=275&goback=%2Efps_PBCK_%22ruby on rails%22_*1_*1_*1_*1_*1_*1_*2_*1_I_us_78746_100_false_6_R_*1_*51_*1_*51_true_CC%2CI%2CN%2CPC%2CED%2CL%2CFG%2CTE%2CFA%2CSE%2CP%2CCS%2CF%2CDR%2CG_*2_4_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2_*2&pvs=ps&trk=pp_profile_name_link";
            string strCompact4LinkedIn = PluginManager.compactifyReferringURL(strToExtractFromPossiblyEncoded4LinkedIn);
            Assert.AreEqual("... fps_PBCK_\"ruby on rails\" ...", strCompact4LinkedIn);

        }

        [Test]
        public void insertLineBreaksIntoURLShouldInsertLineBreaks()
        {
            setAppSettings();

            Console.WriteLine("Starting insertLineBreaksIntoURLShouldInsertLineBreaks. Will insert breaks into CIMG8257BeheadedPlayerBlood ");
            string strToInsertBreaks1ImageNoBreaks = "... CIMG8257BeheadedPlayerBlood.jpg ...";
            string strCompact1url = PluginManager.insertLineBreaksIntoURL(strToInsertBreaks1ImageNoBreaks);
            Assert.AreEqual("... CIMG8257BeheadedPlayerBlood.jpg ...", strCompact1url);
            Console.WriteLine("Will insert breaks into hanging gardens of  babylon image  ");
            string strToInsertBreaks2searchfor = "... searchfor=hanging gardens of  babylon image ... ps=hanging gardens of  babylon image ...";
            string strWithBreaks2searchfor = PluginManager.insertLineBreaksIntoURL(strToInsertBreaks2searchfor);
            Assert.AreEqual("... searchfor=hanging gardens of  babylon image<br/> ... ps=hanging gardens of  babylon image ...", strWithBreaks2searchfor);
            Console.WriteLine("Will insert breaks into hanging gardens of  babylon image  "); Console.WriteLine("Will insert breaks into P1020516KlingonWomen  ");
            string strToInsertBreaks3p = "... p=kligong women ... p=kligong women ... rw=klingon women ... P1020516KlingonWomen.jpg ...";
            string strWithBreaks3p = PluginManager.insertLineBreaksIntoURL(strToInsertBreaks3p);
            Assert.AreEqual("... p=kligong women<br/> ... p=kligong women<br/> ... rw=klingon women<br/> ... P1020516KlingonWomen.jpg ...", strWithBreaks3p);
            Console.WriteLine("Will insert breaks into P1020428ExKlingonAutumnThemed.jpg.html  ");
            string strToInsertBreaks4Slashes = "http://pic.geekitude.com/v/sf/fencon2008/P1020428ExKlingonAutumnThemed.jpg.html";
            string strWithBreaks4Slashes = PluginManager.insertLineBreaksIntoURL(strToInsertBreaks4Slashes);
            Assert.AreEqual("http://pic.geekitude.com/v<br/>/sf/fencon2008/P1020428ExKlingonAutumnThemed.jpg.html", strWithBreaks4Slashes);

            Console.WriteLine("Finished insertLineBreaksIntoURLShouldInsertLineBreaks");
        }
    
    
    }
}
