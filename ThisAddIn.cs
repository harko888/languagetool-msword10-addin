﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Word = Microsoft.Office.Interop.Word;
using Office = Microsoft.Office.Core;
using Microsoft.Office.Tools.Word;
using System.IO;
using System.Web;
using System.Net;
using System.Xml;
using Microsoft.Office.Interop.Word;
using Microsoft.Office.Tools;
using System.Windows.Forms;
using System.Drawing;
using System.Text.RegularExpressions;

namespace languagetool_msword10_addin
{
    public partial class ThisAddIn
    {
        private readonly int maxSuggestions = 10;
        private readonly String LTServer = "https://www.softcatala.org/languagetool/api/checkDocument";
        Word.Application application;
        private TaskPaneControl taskPaneControl1;
        private Microsoft.Office.Tools.CustomTaskPane taskPaneValue;
        private List<int> buttonsIds = new List<int>();
        
        private void ThisAddIn_Startup(object sender, System.EventArgs e)
        {
            application = this.Application;
            application.WindowBeforeRightClick +=
                new Word.ApplicationEvents4_WindowBeforeRightClickEventHandler(application_WindowBeforeRightClick);
            application.DocumentBeforeSave += new Word.ApplicationEvents4_DocumentBeforeSaveEventHandler(application_DocumentBeforeSave);
            application.WindowSelectionChange += new Word.ApplicationEvents4_WindowSelectionChangeEventHandler(application_SelectionChange);

            application.CustomizationContext = application.ActiveDocument;

            taskPaneControl1 = new TaskPaneControl();
            taskPaneValue = this.CustomTaskPanes.Add(taskPaneControl1, "Revisió amb LanguageTool");
            taskPaneValue.VisibleChanged += new EventHandler(taskPaneValue_VisibleChanged);
            taskPaneValue.Visible = false;
            taskPaneValue.Width = 300;
        }

        private void application_SelectionChange(Selection sel)
        {
            if (!sel.Range.GrammarChecked)
            {
                checkCurrentParagraph();
            }
        }

        private void application_DocumentBeforeSave(Word.Document Doc, ref bool SaveAsUI, ref bool Cancel)
        {
            removeAllErrorMarks(Globals.ThisAddIn.Application.ActiveDocument.Content);
        }

        private void taskPaneValue_VisibleChanged(object sender, System.EventArgs e)
        {
            Globals.Ribbons.Ribbon1.toggleButton1.Checked =
                taskPaneValue.Visible;
        }

        public Microsoft.Office.Tools.CustomTaskPane TaskPane
        {
            get
            {
                return taskPaneValue;
            }
        }        

        public void application_WindowBeforeRightClick(Word.Selection selection, ref bool Cancel)
        {
            if (selection != null && !String.IsNullOrEmpty(selection.Text))
            {
                string selectionText = selection.Text;
                Office.CommandBar commandBar = application.CommandBars["Text"];

                foreach (int buttonId in buttonsIds)
                {
                    commandBar.FindControl(Type.Missing, buttonId, Type.Missing , false, false).Delete();
                }
                buttonsIds.Clear();

                if (selection.Font.Underline == WdUnderline.wdUnderlineWavy)
                {
                    Regex regex = new Regex("\\[(.*)\\|(.*)\\|(.*)\\]");
                    Match match = regex.Match(findHiddenData(selection));
                    if (match.Success)
                    {
                        Office.CommandBarButton button1 = (Office.CommandBarButton)commandBar.Controls.Add(Office.MsoControlType.msoControlButton, 1, "info_error", 1, true);
                        button1.Tag = "LTMessage";
                        button1.Caption = match.Groups[1].Value;
                        button1.Enabled = false;
                        button1.Picture = getImage();
                        buttonsIds.Add(button1.Id);

                        String errorStr = match.Groups[3].Value;

                        String[] suggestions = match.Groups[2].Value.Split('#');
                        if (!string.IsNullOrWhiteSpace(suggestions[0]))
                        {
                            int i = 0;
                            while (i<suggestions.Length && i< maxSuggestions) { 
                                Office.CommandBarButton button2 = (Office.CommandBarButton)commandBar.Controls.Add(Office.MsoControlType.msoControlButton, 1, errorStr, i+2, true);
                                button2.Tag = "LTSuggestion" + i;
                                button2.Caption = suggestions[i];
                                buttonsIds.Add(button2.Id);
                                button2.Click +=  new Office._CommandBarButtonEvents_ClickEventHandler(LTbutton_Click);
                                i++;
                            }
                        }
                    }
                }

            }
        }

        public void LTbutton_Click(Office.CommandBarButton ctrl, ref bool cancel)
        {
            if (ctrl == null)
            {
                return;
            }
            //Select underlined words and replace with selected suggestion
            Word.Range rng = Globals.ThisAddIn.Application.Selection.Range;

            int currenSelectionStart = rng.Start;
            int currentSelectionEnd = rng.End;

            //Word.Range rng = selection.Range;
            object findText = Type.Missing; object matchCase = Type.Missing; object matchWholeWord = Type.Missing; object matchWildCards = Type.Missing; object matchSoundsLike = Type.Missing;
            object matchAllWordForms = Type.Missing; object forward = Type.Missing; object wrap = Type.Missing; object format = Type.Missing; object replaceWithText = Type.Missing;
            object replace = Type.Missing; object matchKashida = Type.Missing; object matchDiacritics = Type.Missing; object matchAlefHamza = Type.Missing; object matchControl = Type.Missing;

            wrap = WdFindWrap.wdFindStop;

            rng.Find.ClearFormatting();
            rng.Find.Font.Underline = WdUnderline.wdUnderlineWavy;

            // move forward to find the end of the error
            forward = true;
            rng.Find.Execute(ref findText, ref matchCase, ref matchWholeWord, ref matchWildCards,
                ref matchSoundsLike, ref matchAllWordForms, ref forward, ref wrap, ref format, ref replaceWithText,
                ref replace, ref matchKashida, ref matchDiacritics, ref matchAlefHamza, ref matchControl);
            int rangeEnd = rng.End;

            // move backward to find the start of the error
            forward = false;
            rng.Find.Execute(ref findText, ref matchCase, ref matchWholeWord, ref matchWildCards,
                ref matchSoundsLike, ref matchAllWordForms, ref forward, ref wrap, ref format, ref replaceWithText,
                ref replace, ref matchKashida, ref matchDiacritics, ref matchAlefHamza, ref matchControl);
            int rangeStart = rng.Start;
           
            //replace the error with the suggestion 
            rng.End = rangeEnd;
            rng.Start = rangeStart;
            String errorToReplace = ctrl.Parameter.ToString();
            String textToSearch = rng.Text;
            if (string.IsNullOrWhiteSpace(errorToReplace) || string.IsNullOrWhiteSpace(textToSearch))
                return;
            int indexFound = textToSearch.IndexOf(errorToReplace);
            if (indexFound >= 0)
            {
                rng.Start += indexFound;
                rng.End = rng.Start + errorToReplace.Length;
                rng.Text = ctrl.Caption;
                rng.Font.Underline = WdUnderline.wdUnderlineNone;
            }
        }


        public void checkCurrentParagraph()
        {
            //Checks whole paragraphs in the current selection.            
            Microsoft.Office.Interop.Word.Document Doc = Globals.ThisAddIn.Application.ActiveDocument;
            if (Doc == null || Doc.ReadOnly)
            {
                return;
            }
            
            Word.Range initRng = Globals.ThisAddIn.Application.Selection.Range;
            int rngStart = initRng.Paragraphs.First.Range.Start;
            int rngEnd = initRng.Paragraphs.Last.Range.End;
            Word.Range rangeToCheck = Doc.Range(rngStart, rngEnd);
            checkRange(rangeToCheck);
        }

        private void checkRange(Word.Range rangeToCheck)
        {
            if (string.IsNullOrWhiteSpace(rangeToCheck.Text))
            {
                return;
            }

            removeAllErrorMarks(rangeToCheck); 
            Microsoft.Office.Interop.Word.Document Doc = Globals.ThisAddIn.Application.ActiveDocument;
            String textToCheck = rangeToCheck.Text.ToString();
            String lang = GetLanguageISO(rangeToCheck.LanguageID.ToString());
            
            //String results = GetResultsFrom(uri);
            String checkOptions = "";
            String results = getResultsFromServer(lang, textToCheck, checkOptions);
            //int myParaOffset = 0; // Not necessary if results are processed in reverse order
            int prevErrorStart = -1;
            int prevErrorEnd = -1;
            foreach (Dictionary<string, string> myerror in ParseXMLResults(results).Reverse<Dictionary<string, string>>())
            {
                //Select error start and end
                int offset = int.Parse(myerror["offset"]);
                int errorlength = int.Parse(myerror["errorlength"]);
                int errorStart = rangeToCheck.Start + offset;// + myParaOffset;
                int errorEnd = errorStart + errorlength;
                if (errorEnd == prevErrorEnd)  // Mark just one error at the same place
                {
                    continue;
                }
                Word.Range rng = Doc.Range(errorStart, errorEnd);
                // choose color for underline
                Word.WdColor mycolor = Word.WdColor.wdColorBlue;
                switch (myerror["locqualityissuetype"])
                {
                    case "misspelling":
                        mycolor = Word.WdColor.wdColorRed;
                        break;
                    case "style":
                    case "locale-violation":
                        mycolor = Word.WdColor.wdColorGreen;
                        break;
                }
                // do not track changes
                bool isTrackingRevisions = Doc.TrackRevisions;
                Doc.TrackRevisions = false;
                // unerline errors
                rng.Font.Underline = WdUnderline.wdUnderlineWavy;
                rng.Font.UnderlineColor = mycolor;
                // add hidden data after error. Format: [<error message>|replacement1#replacement2#replacement3...|<error string>]
                string errorData = "[" + myerror["msg"] + "|" + myerror["replacements"] + "|" + myerror["context"].Substring(int.Parse(myerror["contextoffset"]), errorlength) + "]";
                //myParaOffset += errorData.Length;
                Word.Range newRng = Doc.Range(errorEnd, errorEnd); //make it safer!
                newRng.Text = errorData;
                newRng.Font.Hidden = 1;
                newRng.Font.Color = WdColor.wdColorRed;
                //Store previous start and end values
                prevErrorEnd = errorEnd;
                prevErrorStart = errorStart;
                // Track revisions again
                Doc.TrackRevisions = isTrackingRevisions;
            }
            rangeToCheck.GrammarChecked = true; // Wow! This is not a hack. Bravo Microsoft!
        }

        public void checkActiveDocument()
        {
            //Checks the whole document
            //TODO: checking only parts of the document from the cursor
            //TODO: avoid spelling errors in footnote references
            //TODO: Show a message when there are no errors
            Microsoft.Office.Interop.Word.Document Doc = Globals.ThisAddIn.Application.ActiveDocument;
            if (Doc == null || Doc.ReadOnly)
            {
                return;
            }

            removeAllErrorMarks(Doc.Content);
            try
            {
                //checks the whole document by paragraphs
                //TODO: find a better way to divide do document in parts
                Word.Paragraph firstPara = Doc.Paragraphs.First;
                int numParagraphs = Doc.Paragraphs.Count;

                for (int i = 1; i <= numParagraphs; i++)
                {
                    Word.Paragraph para = firstPara.Next(i - 1);
                    Word.Range myrange = para.Range;
                    if (!string.IsNullOrWhiteSpace(myrange.Text.ToString()))
                    { 
                        checkRange(myrange);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message + "," + ex.StackTrace);
            }
        }

        private String findHiddenData(Word.Selection selection)
        {
            //Retrieve hidden data after underlined words.
            Microsoft.Office.Interop.Word.Document Doc = Globals.ThisAddIn.Application.ActiveDocument;
            if (Doc == null || Doc.ReadOnly)
            {
                return "";
            }

            object findText = "(\\[*\\])";
            object matchCase = false;
            object matchWholeWord = false;
            object matchWildCards = true;
            object matchSoundsLike = false;
            object matchAllWordForms = false;
            object forward = true;
            object wrap = WdFindWrap.wdFindStop;
            object format = true;
            object replaceWithText = "\\1";
            object replace = WdReplace.wdReplaceNone;
            object matchKashida = false;
            object matchDiacritics = false;
            object matchAlefHamza = false;
            object matchControl = false;
                        
            Word.Range rng = selection.Range;                   
            rng.Find.ClearFormatting();
            rng.Find.Font.Hidden = 1;
            rng.Find.Replacement.ClearFormatting();
            rng.Find.Replacement.Font.Hidden = 1;

            Globals.ThisAddIn.Application.ScreenUpdating = false;
            bool isShowingHiddenText = Doc.ActiveWindow.View.ShowHiddenText; //Find & replace work better this way!
            Doc.ActiveWindow.View.ShowHiddenText = true;

            //execute find and replace
            bool found = rng.Find.Execute(ref findText, ref matchCase, ref matchWholeWord,
                ref matchWildCards, ref matchSoundsLike, ref matchAllWordForms, ref forward, ref wrap, ref format, ref replaceWithText, ref replace,
                ref matchKashida, ref matchDiacritics, ref matchAlefHamza, ref matchControl);

            String msg = "";
            if (found && rng.Text!= null)
            {
                msg = rng.Text;
            }
            Doc.ActiveWindow.View.ShowHiddenText = isShowingHiddenText;
            Globals.ThisAddIn.Application.ScreenUpdating = true;
            return msg;
        }

        public void removeAllErrorMarks(Word.Range rng)
        {
            if (string.IsNullOrWhiteSpace(rng.Text))
            {
                return;
            }
            Microsoft.Office.Interop.Word.Document Doc = Globals.ThisAddIn.Application.ActiveDocument;
            if (Doc == null || Doc.ReadOnly)
            {
                return;
            }
            bool isTrackingRevisions = Doc.TrackRevisions;
            Doc.TrackRevisions = false;
            Globals.ThisAddIn.Application.ScreenUpdating = false; //Find & replace work better this way!
            //options
            object findText = "";
            object replaceWithText = "";
            object matchCase = false;
            object matchWholeWord = false;
            object matchWildCards = false;
            object matchSoundsLike = false;
            object matchAllWordForms = false;
            object forward = true;
            object format = true;
            object matchKashida = false;
            object matchDiacritics = false;
            object matchAlefHamza = false;
            object matchControl = false;
            object read_only = false;
            object visible = true;
            object replace = WdReplace.wdReplaceAll;
            object wrap = WdFindWrap.wdFindStop;
            
            rng.Find.ClearFormatting();
            rng.Find.Replacement.ClearFormatting();
            rng.Find.Font.Underline = WdUnderline.wdUnderlineWavy;
            rng.Find.Replacement.Font.Underline = WdUnderline.wdUnderlineNone;
            //execute find and replace
            rng.Find.Execute(ref findText, ref matchCase, ref matchWholeWord,
                ref matchWildCards, ref matchSoundsLike, ref matchAllWordForms, ref forward, ref wrap, ref format, ref replaceWithText, ref replace,
                ref matchKashida, ref matchDiacritics, ref matchAlefHamza, ref matchControl);
            //Remove hidden data
            findText = "*";
            replaceWithText = "";
            matchWildCards = true;
            replace = WdReplace.wdReplaceAll;
            wrap = WdFindWrap.wdFindStop;
            rng.Find.ClearFormatting();
            rng.Find.Replacement.ClearFormatting();
            rng.Find.Font.Hidden = 1;
            //execute find and replace
            Doc.ActiveWindow.View.ShowHiddenText = true;
            rng.Find.Execute(ref findText, ref matchCase, ref matchWholeWord,
                ref matchWildCards, ref matchSoundsLike, ref matchAllWordForms, ref forward, ref wrap, ref format, ref replaceWithText, ref replace,
                ref matchKashida, ref matchDiacritics, ref matchAlefHamza, ref matchControl);
            Doc.ActiveWindow.View.ShowHiddenText = false;

            Globals.ThisAddIn.Application.ScreenUpdating = true;
            Doc.TrackRevisions = isTrackingRevisions;
        }
        
        private static List<Dictionary<string, string>> ParseXMLResults(String xmlString)
        {
            XElement xml = XElement.Parse(xmlString);
            var suggestions = new List<Dictionary<string, string>>();

            foreach (var myerror in xml.Descendants("error"))
            {
                var suggestion = new Dictionary<string, string>();
                foreach (var myattribute in myerror.Attributes())
                {
                    suggestion.Add(myattribute.Name.ToString(), myattribute.Value);
                }
                suggestions.Add(suggestion);
            }
            return suggestions;
        }

        //TODO: Find a better way
        private static String GetLanguageISO(String langObj)
        {
            switch (langObj)
            {
                case "wdCatalan":
                    return "ca";
                case "wdEnglishUS":
                    return "en-US";
                default:
                    return ("");
            }
        }

        private string getResultsFromServer(String lang, String textToCheck, String checkOptions)
        {
            String uriString = LTServer + "?language=" + lang + "&text=" + WebUtility.UrlEncode(textToCheck);
            uriString = uriString.Replace("%C2%A0", "+"); // Why?
            Uri uri = new Uri(uriString);
            string result = "";
            try
            {
                // Create the web request  
                System.Net.HttpWebRequest request = System.Net.WebRequest.Create(uri) as System.Net.HttpWebRequest;
                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    // Get the response stream  
                    StreamReader reader = new StreamReader(response.GetResponseStream(), System.Text.Encoding.UTF8);
                    // Read the whole contents and return as a string  
                    result = reader.ReadToEnd();
                }
                return result;
            }
            catch 
            {
                System.Windows.Forms.MessageBox.Show("No es pot contactar amb el servidor: " + LTServer + "."); // + ex.Message + "," + ex.StackTrace
            }
            return "";
        }

        sealed public class ConvertImage : System.Windows.Forms.AxHost
        {
            private ConvertImage()
                : base(null)
            {
            }

            public static stdole.IPictureDisp Convert
                (System.Drawing.Image image)
            {
                return (stdole.IPictureDisp)System.
                    Windows.Forms.AxHost
                    .GetIPictureDispFromPicture(image);
            }
        }
        private stdole.IPictureDisp getImage()
        {
            stdole.IPictureDisp tempImage = null;
            try
            {
                System.Drawing.Icon newIcon =
                    Properties.Resources.LanguageTool_Logo;

                System.Windows.Forms.ImageList newImageList =
                    new System.Windows.Forms.ImageList();
                newImageList.Images.Add(newIcon);
                tempImage = ConvertImage.Convert(newImageList.Images[0]);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
            return tempImage;
        }

        private void ThisAddIn_Shutdown(object sender, System.EventArgs e)
        {
        }

        #region VSTO generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            this.Startup += new System.EventHandler(ThisAddIn_Startup);
            this.Shutdown += new System.EventHandler(ThisAddIn_Shutdown);
        }

        #endregion
    }
}
