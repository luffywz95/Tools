using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using SpreadsheetGear;

namespace DDS2RPV
{
    public class DDS2RPVHelper
    {
        #region Declare
        private ProjectCode projectCode { get; set; }

        public string TranslationResouceFilePath;
        protected SpreadsheetGear.IRange cells;
        protected Dictionary<string, int?> LanguageOptionColumnIndexMap;

        private Dictionary<string, Dictionary<string, string>> CacheDataNamePatternTranslattions;

        private Dictionary<string, Dictionary<string, string>> CacheColumnCaptionTranslattions;

        public string CurrentMessage { get;  private set; }
        #endregion

        #region Properties
        public readonly string[] TranslationLanguageOptions = new[] { "ENG", "GB", "B5", "TW" };

        #region Constants
        private const string FIELD_BLOCK_PATTERN_B12 = ""
            + "<Field>"
            + "\r\n  <DataName>{0}</DataName>"
            + "\r\n  <ColumnName>{1}</ColumnName>"
            + "\r\n  <ColumnCaption>"
            + "\r\n    <LanguageENG>{2}</LanguageENG>"
            + "\r\n    <LanguageCGB>{3}</LanguageCGB>"
            + "\r\n    <LanguageCB5>{4}</LanguageCB5>"
            + "\r\n  </ColumnCaption>"
            + "\r\n  <ColumnDataType>{5}</ColumnDataType>"
            + "\r\n  {6}"
            + "\r\n</Field>";

        private const string FIELD_BLOCK_PATTERN_B14 = ""
            + "<Field>"
            + "\r\n  <DataName>{0}</DataName>"
            + "\r\n  <ColumnName>{1}</ColumnName>"
            + "\r\n  <ColumnCaption>"
            + "\r\n    <LanguageENG>{2}</LanguageENG>"
            + "\r\n    <LanguageCGB>{3}</LanguageCGB>"
            + "\r\n    <LanguageCB5>{4}</LanguageCB5>"
            + "\r\n    <LanguageVET>{5}</LanguageVET>"
            + "\r\n  </ColumnCaption>"
            + "\r\n  <ColumnDataType>{6}</ColumnDataType>"
            + "\r\n  {7}"
            + "\r\n</Field>";
        #endregion
        #endregion

        #region Classes
        public class TableInfo
        {
            public string FileName { get; set; }
            public string TableID { get; set; }
            public string Description { get; set; }
            public string FilePath { get; set; }
            public string RpvDataNamePattern { get; set; }
            public HashSet<RpvFieldInfo> RpvFieldInfos { get; set; }

            public bool IsSelect { get; set; } = false;

            public TableInfo()
            {
                this.FileName = this.TableID = this.Description = this.FilePath = this.RpvDataNamePattern = string.Empty;
                this.RpvFieldInfos = new HashSet<RpvFieldInfo>();
            }
        }

        public class RpvFieldInfo
        {
            #region Declare
            public string DataName { get; set; }
            public string ColumnName { get; set; }
            public string ColumnDesc { get; set; }
            public Dictionary<string, string> ColumnCaptions { get; set; }
            public string ColumnDataType { get; set; }

            #region Column Data Type (Number)
            public string NumFormat { get; set; }
            public int NumFrontDec { get; set; }
            public int NumBackDec { get; set; }
            public bool Visible { get; set; } = false;
            #endregion

            #region Column Data Type (Date)
            public string DateInterval { get; set; } = "Date";
            #endregion
            #endregion

            public RpvFieldInfo()
            {
                DataName = ColumnName = ColumnDataType = NumFormat = string.Empty;
                ColumnCaptions = new Dictionary<string, string>();
                NumFrontDec = NumBackDec = 0;
            }

            public override int GetHashCode()
            {
                return this.DataName.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj != null && obj is RpvFieldInfo)
                {
                    return this.GetHashCode() == ((RpvFieldInfo)obj).GetHashCode();
                }
                return false;
            }
        }
        #endregion

        #region Enumeration
        private enum ProjectCode
        {
            B12,
            B14
        }
        #endregion

        public DDS2RPVHelper()
        {
            this.LanguageOptionColumnIndexMap = new Dictionary<string, int?>();
            this.CacheDataNamePatternTranslattions = new Dictionary<string, Dictionary<string, string>>();
            this.CacheColumnCaptionTranslattions = new Dictionary<string, Dictionary<string, string>>();
        }

        public DDS2RPVHelper(string translationResouceFilePath) : this()
        {
            this.TranslationResouceFilePath = translationResouceFilePath;
        }

        #region Initialization
        public bool Initialize(string projectCodeText)
        {
            if (string.IsNullOrEmpty(projectCodeText))
            {
                this.CurrentMessage = "Please choose a project code.";
                return false;
            }
            else
            {
                ProjectCode projectCode;
                if (Enum.TryParse<ProjectCode>(projectCodeText, out projectCode))
                {
                    this.projectCode = projectCode;
                }
                else
                {
                    this.CurrentMessage = "The chosen project type is not supported.";
                    return false;
                }
            }

            string errorMessage;
            if (!this.ReadTranslationResouceExcelFile(out errorMessage))
            {
                this.CurrentMessage = errorMessage;
                return false;
            }

            return true;
        }

        private bool ReadTranslationResouceExcelFile(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!File.Exists(TranslationResouceFilePath))
            {
                errorMessage = $"Translation resouce file was not found at:\r\n\"{TranslationResouceFilePath}\"";
                return false;
            }

            var workbook = Factory.GetWorkbook(TranslationResouceFilePath);
            var worksheet = workbook.Worksheets["Content"];
            if (worksheet == null) worksheet = workbook.Worksheets[0];
            this.cells = worksheet.UsedRange;

            this.LanguageOptionColumnIndexMap.Clear();
            foreach (var langOption in this.TranslationLanguageOptions)
            {
                this.LanguageOptionColumnIndexMap.Add(langOption, this.cells.Find(langOption, this.cells[0, 0], FindLookIn.Values, LookAt.Whole, SearchOrder.ByRows, SearchDirection.Next, false)?.Column);
            }

            return true;
        }
        #endregion

        #region Public Method
        public bool GetDDSFileTableInfo(FileInfo ddsFileInfo, out TableInfo tableInfo)
        {
            tableInfo = new TableInfo();

            try
            {
                using (StreamReader sr = new StreamReader(ddsFileInfo.FullName, Encoding.UTF8))
                {
                    string content = sr.ReadToEnd();

                    // FileName
                    tableInfo.FileName = ddsFileInfo.Name;

                    // FilePath
                    tableInfo.FilePath = ddsFileInfo.FullName;

                    // Description
                    string pattern_FileDescription = $@"\b{Regex.Escape("FileDescription")}\b\s*\""(?<FileDescription>\w+)[@]*\""";
                    var match_FileDescription = Regex.Match(content, pattern_FileDescription, RegexOptions.IgnoreCase);
                    if (match_FileDescription != null && match_FileDescription.Success)
                    {
                        tableInfo.Description = match_FileDescription.Groups["FileDescription"].Value;
                    }

                    // TableID
                    string pattern_TableId = $@"\b{Regex.Escape("TableID")}\b\s*(?<TableID>\w+)[@]*";
                    var match_TableID = Regex.Match(content, pattern_TableId, RegexOptions.IgnoreCase);
                    if (match_TableID != null && match_TableID.Success)
                    {
                        tableInfo.TableID = match_TableID.Groups["TableID"].Value;
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool ExportRpvFiles(IEnumerable<TableInfo> tableInfos, string destFileDirectory = @"..\@exports")
        {
            this.CacheDataNamePatternTranslattions.Clear();

            foreach (var tableInfo in tableInfos)
            {
                this.ExportOneRpvFile(tableInfo, destFileDirectory);   // tableInfo.FilePath, destFilePath, rpvDataNamePattern
            }
            return true;
        }

        public int GetApproximateRecordSize(TableInfo tableInfo)
        {
            tableInfo.RpvFieldInfos.Clear();
            int approxRecordSize = 0;

            using (StreamReader sr = new StreamReader(tableInfo.FilePath, Encoding.UTF8))
            {
                string content = sr.ReadToEnd();

                string pattern_DataFieldBlock = $@"\b{Regex.Escape("DataField")}\b\s*{{(?<DataField>[^}}]*)}}";

                var match_DataFieldBlock = Regex.Match(content, pattern_DataFieldBlock, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match_DataFieldBlock == null || !match_DataFieldBlock.Success) goto END_OF_METHOD;

                string dataFieldWrapContent = match_DataFieldBlock.Groups["DataField"].Value;
                var dataFields = Regex.Split(dataFieldWrapContent, @"\r\n").Where(substr => substr != string.Empty).ToList();

                RpvFieldInfo rpvFieldInfo;
                string fieldType, fieldFormat, rpvFieldType; int fieldLength;
                foreach (var dataField in dataFields)
                {
                    var match_FieldInfo = Regex.Match(dataField, @"\""(?<FieldDesc>.*?)\""\s*(?<FieldName>\w+)\s*(?<FieldType>\w+)(?<FieldFormat>\(.*?\)).*?$");
                    if (match_FieldInfo.Success)
                    {
                        fieldType = match_FieldInfo.Groups["FieldType"].Value;
                        rpvFieldType = this.GetRPVColumnDataType(fieldType);

                        fieldFormat = match_FieldInfo.Groups["FieldFormat"].Value;

                        rpvFieldInfo = new RpvFieldInfo();
                        if (!string.IsNullOrEmpty(rpvFieldType))
                        {
                            rpvFieldInfo.ColumnName = match_FieldInfo.Groups["FieldName"].Value;
                            rpvFieldInfo.ColumnDesc = match_FieldInfo.Groups["FieldDesc"].Value;
                            rpvFieldInfo.ColumnDataType = fieldType;
                            tableInfo.RpvFieldInfos.Add(rpvFieldInfo);
                        }
                        this.GetRPVNumFormat(rpvFieldInfo, fieldType, fieldFormat);

                        fieldLength = 0;
                        switch (fieldType.ToUpper())
                        {
                            case "NUM":
                                // +1 for the decimal point
                                fieldLength = rpvFieldInfo.NumFrontDec + rpvFieldInfo.NumBackDec + (rpvFieldInfo.NumBackDec > 0 ? 1 : 0);
                                break;
                            case "DATE":
                                {
                                    var match = Regex.Match(fieldFormat, @"\((?<DateLength>\d+)(?:[,\sv]*(?<TimeLength>\d+))?\)");
                                    var tmpLength = 0;
                                    if (match.Groups.ContainsKey("DateLength"))
                                    {
                                        Int32.TryParse(match.Groups["DateLength"].Value, out tmpLength);
                                        fieldLength += tmpLength;
                                    }
                                    if (match.Groups.ContainsKey("TimeLength"))
                                    {
                                        Int32.TryParse(match.Groups["TimeLength"].Value, out tmpLength);
                                        fieldLength += tmpLength;
                                    }
                                }
                                break;
                            case "CHAR":
                            default:
                                {
                                    var match = Regex.Match(fieldFormat, @"\((?<TextLength>\d+)\)");
                                    if (match.Groups.ContainsKey("TextLength"))
                                    {
                                        Int32.TryParse(match.Groups["TextLength"].Value, out fieldLength);
                                    }
                                }
                                break;
                        }
                        approxRecordSize += fieldLength;
                    }
                }
            }
            return approxRecordSize;

        END_OF_METHOD:
            return 0;
        }
        #endregion

        #region Internal Method
        private void ExportOneRpvFile(TableInfo tableInfo, string downloadFileDir) // string ddsFilePath, string downloadFilePath, string dataNamePattern
        {
            string ddsFilePath, dataNamePattern, downloadFilePath;

            ddsFilePath = tableInfo.FilePath;
            dataNamePattern = tableInfo.RpvDataNamePattern.Trim();

            if (!File.Exists(ddsFilePath)) goto END_OF_METHOD;

            if (string.IsNullOrEmpty(dataNamePattern))
            {
                downloadFilePath = Path.Combine(downloadFileDir, "" + this.projectCode, Path.ChangeExtension(tableInfo.FileName, ".rpv"));
            }
            else
            {
                downloadFilePath = Path.Combine(downloadFileDir, "" + this.projectCode, string.Format("{0} ({1}).rpv", Path.GetFileNameWithoutExtension(tableInfo.FileName), dataNamePattern));
            }
            if (!Directory.Exists(Path.GetDirectoryName(downloadFilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(downloadFilePath));

            using (StreamReader sr = new StreamReader(ddsFilePath, Encoding.UTF8))
            {
                string content = sr.ReadToEnd();

                // this.CacheDataNamePatternTranslattion

                string pattern_TableId = $@"\b{Regex.Escape("TableID")}\b\s*(?<TableID>\w+)[@]*";
                string pattern_DataFieldBlock = $@"\b{Regex.Escape("DataField")}\b\s*{{(?<DataField>[^}}]*)}}";

                var match_TableID = Regex.Match(content, pattern_TableId, RegexOptions.IgnoreCase);
                if (match_TableID == null || !match_TableID.Success) goto END_OF_METHOD;

                var match_DataFieldBlock = Regex.Match(content, pattern_DataFieldBlock, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match_DataFieldBlock == null || !match_DataFieldBlock.Success) goto END_OF_METHOD;

                string tableID = match_TableID.Groups["TableID"].Value;
                string dataFieldWrapContent = match_DataFieldBlock.Groups["DataField"].Value;
                var dataFields = Regex.Split(dataFieldWrapContent, @"\r\n").Where(substr => substr != string.Empty).ToList();

                HashSet<RpvFieldInfo> rpvFieldInfos = new HashSet<RpvFieldInfo>();
                RpvFieldInfo rpvFieldInfo;

                string fieldName, columnCaptionENG, columnDataType;
                Dictionary<string, string> columnCaptionTranslations, dataNamePatternTranslations;
                Dictionary<string , Dictionary<string, string>> dataNamePatternTranslationMap = new Dictionary<string, Dictionary<string, string>>();

                foreach (var dataField in dataFields)
                {
                    var match_FieldInfo = Regex.Match(dataField, @"\""(?<FieldDesc>.*?)\""\s*(?<FieldName>\w+)\s*(?<FieldType>\w+)(?<FieldFormat>\(.*?\)).*?$");
                    if (match_FieldInfo.Success)
                    {

                        columnDataType = this.GetRPVColumnDataType(match_FieldInfo.Groups["FieldType"].Value);
                        if (string.IsNullOrEmpty(columnDataType)) continue;

                        rpvFieldInfo = new RpvFieldInfo();

                        // DataName
                        if (string.IsNullOrEmpty(dataNamePattern) || dataNamePattern == "$1")
                        {
                            rpvFieldInfo.DataName = $"{tableID}_{fieldName = match_FieldInfo.Groups["FieldName"].Value}";
                        }
                        else
                        {
                            rpvFieldInfo.DataName = Regex.Replace($"{tableID}_{fieldName = match_FieldInfo.Groups["FieldName"].Value}", @"(^.*?$)", dataNamePattern);
                        }
                        rpvFieldInfo.ColumnName = rpvFieldInfo.DataName;

                        if (!rpvFieldInfos.Add(rpvFieldInfo)) continue;

                        // ColumnDataType
                        rpvFieldInfo.ColumnDataType = columnDataType;

                        // ColumnCaption [ENG, CGB, CB5, VET (i.e. TW)]
                        rpvFieldInfo.ColumnDesc = columnCaptionENG = match_FieldInfo.Groups["FieldDesc"].Value;

                        #region [Obsolete]
                        // ColumnCaption - Except ENG
                        //SpreadsheetGear.IRange resultCell;

                        //bool isTranslationFound =
                        //    (resultCell = this.cells.Find(fieldName, this.cells[0, 0], FindLookIn.Values, LookAt.Whole, SearchOrder.ByColumns, SearchDirection.Next, false)) != null
                        //    || (resultCell = this.cells.Find(columnCaptionENG, this.cells[0, 0], FindLookIn.Values, LookAt.Whole, SearchOrder.ByColumns, SearchDirection.Next, false)) != null;
                        //foreach (var kvp in this.ColumnCaptionLanguageMap)
                        //{
                        //    if (kvp.Key == "ENG") continue;

                        //    if (isTranslationFound)
                        //    {
                        //        int? columnIndex = this.LanguageOptionColumnIndexMap[kvp.Value];
                        //        if (columnIndex != null)
                        //        {
                        //            rpvFieldInfo.ColumnCaptions.Add(kvp.Key, this.cells[resultCell.Row, columnIndex.Value].Text);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        rpvFieldInfo.ColumnCaptions.Add(kvp.Key, columnCaptionENG);
                        //    }
                        //}
                        #endregion

                        this.GetColumnCaptionTranslations(new[] { columnCaptionENG, fieldName }, this.TranslationLanguageOptions, columnCaptionENG, out columnCaptionTranslations);

                        if (!string.IsNullOrEmpty(dataNamePattern))
                        {
                            dataNamePatternTranslationMap.Clear();

                            // Split the DataNamePattern by non-alphanumeric characters
                            var split_DataNamePattern = Regex.Split(dataNamePattern, "[^(0-9a-zA-Z$)]");

                            // Get translations for each split value
                            foreach (var splitValue in split_DataNamePattern)
                            {
                                if (splitValue == "$1") continue;

                                this.GetDataNamePatternTranslations(splitValue, this.TranslationLanguageOptions, out dataNamePatternTranslations);
                                dataNamePatternTranslationMap.Add(splitValue, dataNamePatternTranslations);
                            }

                            Dictionary<string, string> actualColumnCaptionTranslations = new Dictionary<string, string>();
                            foreach (var lang in TranslationLanguageOptions)
                            {
                                Dictionary<string, string> translations;
                                string fullTranslationWithoutDataName = Regex.Replace(dataNamePattern + " ", "(?<Split>.*?)[^(0-9a-zA-Z$)]", new MatchEvaluator((match) =>
                                {
                                    var splitValue = match.Groups["Split"].Value;

                                    if (string.IsNullOrEmpty(splitValue)) return string.Empty;
                                    if (splitValue == "$1") return "$1";

                                    dataNamePatternTranslationMap.TryGetValue(splitValue, out translations);
                                    if (translations == null) return splitValue;

                                    switch (lang)
                                    {
                                        case "ENG":
                                            return translations[lang] + " ";
                                        default:
                                            return translations[lang];
                                    }
                                })).Trim();

                                actualColumnCaptionTranslations.Add(lang, fullTranslationWithoutDataName);
                            }


                            foreach (var kvp in columnCaptionTranslations)
                            {
                                string columnCaption = Regex.Replace(kvp.Value, @"(^.*?$)", actualColumnCaptionTranslations[kvp.Key]);
                                rpvFieldInfo.ColumnCaptions.Add(kvp.Key, columnCaption);
                            }
                        }
                        else
                        {
                            foreach (var kvp in columnCaptionTranslations)
                            {
                                rpvFieldInfo.ColumnCaptions.Add(kvp.Key, kvp.Value);
                            }
                        }

                        // NumFormat
                        this.GetRPVNumFormat(rpvFieldInfo, columnDataType, match_FieldInfo.Groups["FieldFormat"].Value);
                    }
                }

                using (StreamWriter sw = new StreamWriter(downloadFilePath, false, Encoding.UTF8))
                {
                    sw.Write(this.GetRPVFieldBlockContent(rpvFieldInfos));
                }
            }

        END_OF_METHOD:
            return;
        }

        private void GetDataNamePatternTranslations(string dataNamePattern, string[] langs, out Dictionary<string, string> translations)
        {
            if (this.CacheDataNamePatternTranslattions.ContainsKey(dataNamePattern))
            {
                translations = this.CacheDataNamePatternTranslattions[dataNamePattern];
            }
            else
            {
                this.FindTranslations(new[] { dataNamePattern }, langs, dataNamePattern, out translations);
                this.CacheDataNamePatternTranslattions.Add(dataNamePattern, translations);
            }
        }
        private void GetColumnCaptionTranslations(string[] searches, string[] langs, string defaultCaption, out Dictionary<string, string> translations)
        {
            if (searches.Length == 0)
            {
                translations = langs.ToDictionary(_ => _, _ => defaultCaption);
                return;
            }

            foreach (var search in searches)
            {
                if (this.CacheColumnCaptionTranslattions.ContainsKey(search))
                {
                    translations = this.CacheColumnCaptionTranslattions[search];
                    return;
                }
            }

            string searchTarget;
            if (this.FindTranslations(searches, langs, defaultCaption, out translations, out searchTarget))
            {
                this.CacheColumnCaptionTranslattions.Add(searchTarget, translations);
            }
            else
            {
                if (!this.CacheColumnCaptionTranslattions.ContainsKey(searches[0]))
                {
                    this.CacheColumnCaptionTranslattions.Add(searches[0], translations = langs.ToDictionary(_ => _, _ => defaultCaption));
                }
            }
        }

        private bool FindTranslations(string[] searches, string[] langs, string defaultTranslation, out Dictionary<string, string> translations)
        {
            string searchTarget;
            return this.FindTranslations(searches, langs, defaultTranslation, out translations, out searchTarget);
        }
        private bool FindTranslations(string[] searches, string[] langs, string defaultTranslation, out Dictionary<string, string> translations, out string searchTarget)
        {
            translations = new Dictionary<string, string>();
            searchTarget = string.Empty;

            SpreadsheetGear.IRange resultCell = null;
            bool isTranslationFound = false;

            foreach (var search in searches)
            {
                resultCell = this.cells.Find(search, this.cells[0, 0], FindLookIn.Values, LookAt.Whole, SearchOrder.ByColumns, SearchDirection.Next, false);
                if (resultCell != null)
                {
                    isTranslationFound = true;
                    searchTarget = search;
                    break;
                }
            }

            if (isTranslationFound)
            {
                foreach (var lang in langs)
                {
                    int? columnIndex = this.LanguageOptionColumnIndexMap[lang];

                    if (columnIndex != null)
                    {
                        translations.Add(lang, this.cells[resultCell.Row, columnIndex.Value].Text);
                    }
                }
            }

            return isTranslationFound;
        }

        private string GetRPVFieldBlockContent(HashSet<RpvFieldInfo> rpvFieldInfos)
        {
            StringBuilder result = new StringBuilder();

            switch (this.projectCode)
            {
                case ProjectCode.B12:
                    foreach (var rpvFieldInfo in rpvFieldInfos)
                    {
                        var blockContent = string.Format(FIELD_BLOCK_PATTERN_B12,
                            rpvFieldInfo.DataName,                  // {0}: DataName
                            rpvFieldInfo.ColumnName,                // {1}: ColumnName
                            rpvFieldInfo.ColumnCaptions["ENG"],     // {2}: ENG: ColumnCaption - LanguageENG
                            rpvFieldInfo.ColumnCaptions["GB"],      // {3}: GB : ColumnCaption - LanguageCGB
                            rpvFieldInfo.ColumnCaptions["B5"],      // {4}: B5 : ColumnCaption - LanguageCB5
                            rpvFieldInfo.ColumnDataType,            // {6}: ColumnDataType
                            this.GetRPVDataFormatBlock(rpvFieldInfo)
                            );
                        result.AppendLine(blockContent);
                    }
                    break;
                case ProjectCode.B14:
                    foreach (var rpvFieldInfo in rpvFieldInfos)
                    {
                        var blockContent = string.Format(FIELD_BLOCK_PATTERN_B14,
                            rpvFieldInfo.DataName,                  // {0}: DataName
                            rpvFieldInfo.ColumnName,                // {1}: ColumnName
                            rpvFieldInfo.ColumnCaptions["ENG"],     // {2}: ENG: ColumnCaption - LanguageENG
                            rpvFieldInfo.ColumnCaptions["GB"],      // {3}: GB : ColumnCaption - LanguageCGB
                            rpvFieldInfo.ColumnCaptions["B5"],      // {4}: B5 : ColumnCaption - LanguageCB5
                            rpvFieldInfo.ColumnCaptions["TW"],      // {5}: TW : ColumnCaption - LanguageVET
                            rpvFieldInfo.ColumnDataType,            // {6}: ColumnDataType
                            this.GetRPVDataFormatBlock(rpvFieldInfo)
                            );
                        result.AppendLine(blockContent);
                    }
                    break;
            }

            return result.ToString();
        }

        private string GetRPVColumnDataType(string fieldType)
        {
            switch (fieldType.ToUpper())
            {
                case "CHAR":
                    return "string";
                case "NUM":
                    return "num";
                case "DATE":
                    return "date";
                default:
                    return string.Empty;
            }
        }

        private void GetRPVNumFormat(RpvFieldInfo rpvFieldInfo, string columnDataType, string fieldFormat)
        {
            switch (columnDataType.ToUpper())
            {
                case "NUM":
                    {
                        var match = Regex.Match(fieldFormat, @"\((?<FrontDec>\d+)(?:[,\sv]*(?<BackDec>\d+))?\)");
                        if (match.Success)
                        {
                            int frontDec, backDec;
                            if (Int32.TryParse(match.Groups["FrontDec"].Value, out frontDec)
                                && Int32.TryParse(match.Groups["BackDec"].Value, out backDec))
                            {
                                rpvFieldInfo.NumFrontDec = frontDec;
                                rpvFieldInfo.NumBackDec = backDec;
                                string numFormat = string.Join(",", Regex.Split(new string('#', frontDec), @"(###|##|#)").Where(s => s != "").ToArray().Reverse()).ReplaceLast("#", "0");
                                if (backDec > 0)
                                {
                                    numFormat += "." + new string('0', backDec);
                                }
                                rpvFieldInfo.NumFormat = numFormat;
                            }
                        }
                    }
                    break;
                default:
                    return;
            }
        }

        private string GetRPVDataFormatBlock(RpvFieldInfo rpvFieldInfo)
        {
            switch (rpvFieldInfo.ColumnDataType.ToUpper())
            {
                case "DATE":
                    return string.Format(""
                        + "<Date>"
                        + "\r\n    <DateInterval>{0}</DateInterval>"     // by default, using "Date" as <DateInterval/> value;
                                                                       // others such as DateYear, DateMonth and DateDay, can be added if necessary
                        + "\r\n    <Visible>{1}</Visible>"
                        + "\r\n  </Date>",
                            rpvFieldInfo.DateInterval,
                            rpvFieldInfo.Visible ? "True" : "False");
                case "NUM":
                    return string.Format(""
                        + "<NumFormat>{0}</NumFormat>"
                        + "\r\n  <NumFrontDec>{1}</NumFrontDec>"
                        + "\r\n  <NumBackDec>{2}</NumBackDec>"
                        + "\r\n  <Visible>{3}</Visible>",
                            rpvFieldInfo.NumFormat,
                            rpvFieldInfo.NumFrontDec,
                            rpvFieldInfo.NumBackDec,
                            rpvFieldInfo.Visible ? "True" : "False");
                case "STRING":
                default:
                    return string.Format(""
                        + "<StringFormat>"
                        + "\r\n  </StringFormat>"
                        + "\r\n  <Visible>{0}</Visible>",
                            rpvFieldInfo.Visible ? "True" : "False");
            }
        }
        #endregion
    }

    public static class Extension
    {
        public static string ReplaceLast(this string text, string findWhat, string replaceBy)
        {
            string result = string.Empty;
            int lastOccurIndex = text.LastIndexOf(findWhat);
            if (lastOccurIndex != -1)
            {
                result = text.Substring(0, lastOccurIndex) + replaceBy + text.Substring(lastOccurIndex + findWhat.Length);
            }
            return result;
        }
    }
}
