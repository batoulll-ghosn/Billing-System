﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Linq;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using Be.Timvw.Framework.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace AgricoveBilling
{
    public partial class AgricoveBilling : Form
    {
        //--------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Variable Section
        //

        //Set arrays for invoice elements for easy iteration
        TextBox [ ] descBox;
        NumericUpDown [ ] qtyBox;
        ComboBox [ ] unitBox;
        NumericUpDown [ ] uBox;
        Label [ ] tBox;

        bool paid_token = false;            //This one determines whether amount paid has been manually entered
        decimal paid_max = 0;               //This one holds the max paid amount that has been manually entered
        ulong edit_check = 0;               //This is a binary number whose each bit represents a field in the form. a bit being 1 means it contains manually entered data. Setting the LSB to 1 and everything else to zero disables it

        //the following flag makes sure event binder can't bind multiple instances of same event to a control. 0 means no mode set. 1 means fast load mode. 2 means history mode. 3 means recusrive load mode
        int toggle_flag = 0;       

        const int VK_TAB = 0x09; //Tab key
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        //keyboard interrupts for shortcut keys
        [System.Runtime.InteropServices.DllImport ( "user32.dll" )]
        public static extern int SendMessage( IntPtr hWnd , int Msg , int wParam , int lParam );
        [System.Runtime.InteropServices.DllImport ( "user32.dll" )]
        public static extern void keybd_event( byte bVk , byte bScan , uint dwFlags , uint dwExtraInfo );
        [System.Runtime.InteropServices.DllImport ( "user32.dll" )]
        public static extern bool ReleaseCapture();

        //Take a set of buttons for gridview
        DataGridViewButtonColumn ButtonColumn = new DataGridViewButtonColumn ();
        DataGridViewComboBoxColumn ComboColumn = new DataGridViewComboBoxColumn ();
        SplashForm splashForm = new SplashForm ();

        //Print
        private PrintDocument printDocument1 = new PrintDocument ();
        Bitmap memoryImage;
        //

        //Array to hold color scheme for invoice table
        Color [ , ] bgColors = new Color [ 12 , 5 ] {
            { SystemColors.ControlDarkDark , SystemColors.ControlDarkDark, SystemColors.ControlDarkDark, SystemColors.ControlDarkDark, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace , SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace , SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace , SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace , SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark },
            { SystemColors.ButtonFace , SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ButtonFace, SystemColors.ControlDarkDark }
        };

        //--------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Constructor Phase
        //

        public AgricoveBilling()
        {
            InitializeComponent ();

            // loading form elements in the arrays
            descBox = new TextBox [ ] { descBox1 , descBox2 , descBox3 , descBox4 , descBox5 , descBox6 , descBox7 , descBox8 , descBox9 , descBox10 , descBox11 };
            qtyBox = new NumericUpDown [ ] { qtyBox1 , qtyBox2 , qtyBox3 , qtyBox4 , qtyBox5 , qtyBox6 , qtyBox7 , qtyBox8 , qtyBox9 , qtyBox10 , qtyBox11 };
            unitBox = new ComboBox [ ] { unitBox1 , unitBox2 , unitBox3 , unitBox4 , unitBox5 , unitBox6 , unitBox7 , unitBox8 , unitBox9 , unitBox10 , unitBox11 };
            uBox = new NumericUpDown [ ] { uBox1 , uBox2 , uBox3 , uBox4 , uBox5 , uBox6 , uBox7 , uBox8 , uBox9 , uBox10 , uBox11 };
            tBox = new Label [ ] { tBox1 , tBox2 , tBox3 , tBox4 , tBox5 , tBox6 , tBox7 , tBox8 , tBox9 , tBox10 , tBox11 };

            //this.AutoScroll = true;
            this.AutoSize = false;  //disable autosizing of window
        }

        //--------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Initialization Functions
        //

        //Form Load
        private void AgricoveBilling_Load( object sender , EventArgs e )
        {
            //loading screen
            Thread thread = new Thread ( new ThreadStart ( delegate {  System.Windows.Forms.Application.Run ( splashForm ); } ) );
            thread.IsBackground = true;
            thread.SetApartmentState ( ApartmentState.STA );
            thread.Start ();

            toggle_events("fast_load", true);                                                   //make sure unnecessary calculations don't get triggered
            splashForm.SetText ( "Loading Indexes..." );                        //this function reports stage of loading
            add_tabindex ();
            splashForm.SetText ( "Loading Arrays..." );
            add_tag ( descBox );
            add_tag ( qtyBox );
            add_tag ( unitBox );
            add_tag ( uBox );
            add_tag ( tBox );
            splashForm.SetText ( "Loading Database..." );
            populate_list ();                                                    //autocomplete suggestions are loaded
            splashForm.SetText ( "Loading Defaults..." );
            cleanform ();
            splashForm.SetText ( "Reading Configurations..." );
            initiate_label ();

            //Print
            //printDocument1.PrintPage += new PrintPageEventHandler(printDocument1_PrintPage);
            //printDocument1.DefaultPageSettings.Landscape = true;
            //PrinterSettings ps = new PrinterSettings();
            //printDocument1.PrinterSettings = ps;

            //printDocument1.DefaultPageSettings.PaperSize = GetPaperSize("A4");
            //

            splashForm.SetText ( "Loading Menus..." );
            add_context ( descBox );                           //call context menu adder

            splashForm.SetText ( "Setting Parameters..." );
            //MessageBoxManager.OK = "Alright";
            MessageBoxManager.Yes = "Print";                //Turn Yes into a print button using MessageBox manager
            //MessageBoxManager.No = "Nope";
            MessageBoxManager.Register ();

            //find_gridview.Columns.Add(ButtonColumn);                          //this needs to be added dynamically later
            //vScrollBar1.Enabled = true;

            ButtonColumn.Text = "deledit";
            ButtonColumn.Name = "deledit";
            ButtonColumn.UseColumnTextForButtonValue = true;
            ButtonColumn.FlatStyle = FlatStyle.Standard;

            ComboColumn.HeaderText = "Unit";
            ComboColumn.Name = "Unit";
            ComboColumn.Items.Add ( "KG" );
            ComboColumn.Items.Add ( "Gram" );
            ComboColumn.Items.Add ( "Piece" );

            splashForm.SetText ( "Loading Styles.." );
            foreach ( var v in uBox )
            {
                nud_style ( v );
            }
            foreach ( var v in qtyBox )
            {
                nud_style ( v );
            }
            nud_style ( discval );
            nud_style ( txrt );
            nud_style ( paid );
            datagridview_style ( find_gridview );
            ComboColumn.FlatStyle = ButtonColumn.FlatStyle = FlatStyle.Flat;

            toggle_events ( "fast_load" , false );

            splashForm.SetText ( "Done" );              //setting the text to "Done" closes the loading screen

            //This code brings this window to front: By Shabbir Hussain StackOverFlow
            this.WindowState = FormWindowState.Minimized;
            this.Show ();
            this.WindowState = FormWindowState.Normal;
        }

        //This cleans all user inputs on the form
        private void cleanform()
        {

            try
            {
                splashForm.SetText ( "Clearing Form..." );
            }
            catch { }
            //invno.Text = "#INV" + System.DateTime.Now.Date.ToString("dd") + System.DateTime.Now.Date.ToString("MM") + System.DateTime.Now.Date.ToString("yy");
            //invdt.Text = System.DateTime.Now.Date.ToString("dd/MM/yyyy");

            paid_token = false;
            edit_check = 0;
            search.Enabled = true;                      //Find button stays enabled until user has started filling the invoice

            //Save and Print stay disabled. Save is enabled once paid has value, print is enabled when result is loaded from search
            save.Enabled = false;
            print.Enabled = false;

            searchpanel.Visible = false;            //hide find panel

            //set defualts of search panel
            datestart.Enabled = false;
            dateend.Enabled = false;
            dtrange.Checked = false;
            search_duedt.Checked = false;

            Action<Control.ControlCollection> func = null;
            func = ( controls ) =>
            {
                foreach ( Control control in controls )
                    if ( control is TextBox )
                    {
                        ( control as TextBox ).Clear ();
                    }
                    else if ( control is NumericUpDown )
                    {
                        ( control as NumericUpDown ).Value = 0;
                    }
                    else if ( control is DateTimePicker )
                    {
                        ( control as DateTimePicker ).Value = DateTime.Now;
                    }
                    else if ( control is CheckBox )
                    {
                        ( control as CheckBox ).Checked = false;
                    }
                    else if ( control is ComboBox )                            //The default of every combobox must be at 0
                    {
                        if ( ( control as ComboBox ).Items.Count > 0 )
                        {
                            ( control as ComboBox ).SelectedIndex = 0;
                        }
                    }
                    else if ( control is Label )
                    {
                        var r = new Regex ( @"tBox\d{0,2}" );
                        if ( r.IsMatch ( control.Name ) )
                        {
                            ( control as Label ).Text = "0.00";
                        }
                        if ( control.Name == "subtotal" )
                        {
                            ( control as Label ).Text = "0.00";
                        }
                        if ( control.Name == "subttllssdisc" )
                        {
                            ( control as Label ).Text = "0.00";
                        }
                        if ( control.Name == "ttltax" )
                        {
                            ( control as Label ).Text = "0.00";
                        }
                        if ( control.Name == "grssttl" )
                        {
                            ( control as Label ).Text = "0.00";
                        }
                        if ( control.Name == "balancedue" )
                        {
                            ( control as Label ).Text = "0.00";
                        }
                    }
                    else
                    {
                        func ( control.Controls );
                    }
            };

            func ( Controls );
            foreach( NumericUpDown n in qtyBox )                    //must reset the decimal places in qty
            {
                n.DecimalPlaces = 3;
            }

            disable_rows ();
            this.ActiveControl = descBox1;            //set focus to descbox1

            //Assign default values of fields after this line
            search.Text = "&Find";                                  //This text changes between "Find", "back to invoice", and "back to search results" depending on context. "&" is used to imply keyboard shortcut
            find_gridview.DataSource = null;                        //unload the data loaded into gridview due to datasrc_selectedindexchanged event triggered during resetting it
            discval.Value = 0;
            discval.DecimalPlaces = 2;
            discval.Maximum = 99999999;

            DateTime foo = DateTime.UtcNow;
            long unixTime = ( ( DateTimeOffset ) foo ).ToUnixTimeSeconds ();
            invno.Text = "#INV" + unixTime.ToString ();                          //invoice ID is UNIX datetime

            invdt.Value = DateTime.Now;
            ddt.MinDate = invdt.Value;

        }

        //This disbles all rows in invoice table that are unused and changes the bgcolor of cells to imply which fields are disabled
        private void disable_rows()
        {
            bool firstblank = true;        //this will get disabled after hitting first blank row        

            foreach ( var desc in descBox )
            {
                var v = Convert.ToInt32 ( desc.Tag.ToString () );
                if ( desc.Text.Length == 0 )
                {
                    if ( firstblank )
                    {
                        desc.Enabled = true;
                        bgColors [ v + 1 , 0 ] = SystemColors.ControlLightLight;

                        qtyBox [ v ].Enabled = true;
                        bgColors [ v + 1 , 1 ] = SystemColors.ControlLightLight;

                        unitBox [ v ].Enabled = true;
                        bgColors [ v + 1 , 2 ] = SystemColors.ControlLightLight;

                        uBox [ v ].Enabled = true;
                        bgColors [ v + 1 , 3 ] = SystemColors.ControlLightLight;

                        firstblank = false;                                         //all blank rows after this will get disabled
                    }
                    else
                    {
                        descBox [ v ].Enabled = false;
                        bgColors [ v + 1 , 0 ] = SystemColors.ButtonFace;

                        qtyBox [ v ].Enabled = false;
                        bgColors [ v + 1 , 1 ] = SystemColors.ButtonFace;

                        unitBox [ v ].Enabled = false;
                        bgColors [ v + 1 , 2 ] = SystemColors.ButtonFace;

                        uBox [ v ].Enabled = false;
                        bgColors [ v + 1 , 3 ] = SystemColors.ButtonFace;
                    }
                }
                else
                {
                    desc.Enabled = true;
                    bgColors [ v + 1 , 0 ] = SystemColors.ControlLightLight;

                    qtyBox [ v ].Enabled = true;
                    bgColors [ v + 1 , 1 ] = SystemColors.ControlLightLight;

                    unitBox [ v ].Enabled = true;
                    bgColors [ v + 1 , 2 ] = SystemColors.ControlLightLight;

                    uBox [ v ].Enabled = true;
                    bgColors [ v + 1 , 3 ] = SystemColors.ControlLightLight;
                }
            }
            invoice_table.Refresh ();                    //reloads invoice table design
        }

        //load autocomplete suggestions
        private void populate_list()
        {
            using ( var dataContext = new DBConnection () )
            {
                try
                {
                    bool b = false;
                    try
                    { 
                        splashForm.SetText ( "Loading Items..." );                  //try because it will work only during form load
                        b = true;                                                   //from this point onwards, the value of b will determine whether this is form load
                    }
                    catch { }
                    var items = dataContext.Items.ToList ();
                    var desc = items.Select ( t => t.ItemName ).ToList ();
                    var c = desc.Count ();
                    foreach ( var descval in desc )
                    {
                        if ( b )
                        {
                            splashForm.SetText ( "Loading " + c.ToString () + " Items..." );
                        }
                        foreach ( var dBox in descBox )
                        {
                            dBox.AutoCompleteCustomSource.Add ( descval );
                        }
                        c = c - 1;
                    }
                    if ( b )
                    {
                        splashForm.SetText ( "Loading Invoices..." );
                    }
                    var invoice = dataContext.Invoice.ToList ();
                    var names = invoice.Select ( n => n.BillToName ).ToList ();
                    var adds = invoice.Select ( a => a.BillToAdd ).ToList ();
                    c = names.Count ();
                    foreach ( var name in names )
                    {
                        billname.AutoCompleteCustomSource.Add ( name );
                        if ( b )
                        {
                            splashForm.SetText ( "Loading " + c.ToString () + " Invoices..." );
                        }
                        c = c - 1;
                    }
                    foreach ( var add in adds )
                    {
                        billaddr.AutoCompleteCustomSource.Add ( add );
                    }
                }
                catch
                {
                    MessageBox.Show ( "       Database Corrupted    " , "Error" , MessageBoxButtons.OK , MessageBoxIcon.Warning , MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly );                  //Failed to load datacontext. Data in DB doesn't match interface. Extra space to make the messagebox a bit pleasing. Left side needs more space to look symmetrical. DefaultDesktopOnly puts the messagebox above splash screen (Trick by MikeDub StackOverFlow)
                    this.Close ();
                }
                //dataContext.SaveChanges();                
            }
        }

        //this code unloads all events from firing unnecesssarily then reloads it
        private void toggle_events( string s , bool b )
        {
           if ( b && toggle_flag == 0 )                //true means fast load is true. a mode can be enabled only if current mode is 0
            {
                if ( s == "fast_load" )
                {
                    billname.Enter -= textbox_Enter;
                    billname.Leave -= billname_Leave;
                    billname.MouseUp -= textbox_Enter;
                    billname.TextChanged -= billname_TextChanged;
                    billaddr.Enter -= textbox_Enter;
                    billaddr.MouseUp -= textbox_Enter;
                    billaddr.TextChanged -= billaddr_TextChanged;
                    invdt.ValueChanged -= datetime_TextChanged;
                    ddt.ValueChanged -= datetime_TextChanged;

                    foreach ( TextBox t in descBox )
                    {
                        t.Leave -= descBox_LostFocus;
                        t.Enter -= textbox_Enter;
                        t.MouseUp -= textbox_Enter;
                        t.TextChanged -= desc_TextChanged;
                    }

                    foreach ( NumericUpDown n in qtyBox )
                    {
                        n.ValueChanged -= qtyBox_ValueChanged;
                        n.Enter -= numBox_Enter;
                        n.MouseUp -= numBox_Enter;
                        n.Leave -= updown_leave;
                    }

                    foreach ( ComboBox c in unitBox )
                    {
                        c.SelectedIndexChanged -= unitBox_SelectedIndexChanged;
                    }

                    foreach ( NumericUpDown n in uBox )
                    {
                        n.ValueChanged -= uBox_ValueChanged;
                        n.Enter -= numBox_Enter;
                        n.MouseUp -= numBox_Enter;
                        n.Leave -= updown_leave;
                    }

                    subtotal.TextChanged -= disc_calc;
                    discval.ValueChanged -= disc_calc;
                    discval.Enter -= numBox_Enter;
                    discval.Leave -= updown_leave;
                    discval.MouseUp -= numBox_Enter;
                    disctype.SelectedIndexChanged -= disctype_SelectedIndexChanged;
                    subttllssdisc.TextChanged -= txrt_ValueChanged;
                    txrt.ValueChanged -= txrt_ValueChanged;
                    txrt.Enter -= numBox_Enter;
                    txrt.Leave -= updown_leave;
                    txrt.MouseUp -= numBox_Enter;
                    paid.ValueChanged -= paid_ValueChanged;
                    paid.Enter -= numBox_Enter;
                    paid.Leave -= updown_leave;
                    paid.MouseUp -= numBox_Enter;
                    grssttl.TextChanged -= grssttl_TextChanged;
                    datasrc.SelectedIndexChanged -= Datasrc_SelectedIndexChanged;
                    search_name_box.TextChanged -= textbox_Enter;
                    search_inv_box.TextChanged -= textbox_Enter;
                    dtrange.CheckedChanged -= Dtrange_CheckedChanged;
                    search_duedt.CheckedChanged -= search_duedt_CheckedChanged;

                    toggle_flag = 1;                                        //current mode set
                }
                else if ( s == "history_load" )
                {
                    billname.Enter -= textbox_Enter;
                    billname.Leave -= billname_Leave;
                    billname.MouseUp -= textbox_Enter;
                    billname.TextChanged -= billname_TextChanged;
                    billaddr.Enter -= textbox_Enter;
                    billaddr.MouseUp -= textbox_Enter;
                    billaddr.TextChanged -= billaddr_TextChanged;

                    foreach ( TextBox t in descBox )
                    {
                        t.Leave -= descBox_LostFocus;
                        t.Enter -= textbox_Enter;
                        t.MouseUp -= textbox_Enter;
                        t.TextChanged -= desc_TextChanged;
                    }

                    foreach ( NumericUpDown n in qtyBox )
                    {
                        n.ValueChanged -= qtyBox_ValueChanged;
                        n.Enter -= numBox_Enter;
                        n.MouseUp -= numBox_Enter;
                        n.Leave -= updown_leave;
                    }

                    grssttl.TextChanged -= grssttl_TextChanged;
                    datasrc.SelectedIndexChanged -= Datasrc_SelectedIndexChanged;

                    toggle_flag = 2;                                        //current mode set
                }
                else if ( s == "recursive" )
                {
                    foreach ( TextBox t in descBox )
                    {
                        t.Leave -= descBox_LostFocus;
                        t.Enter -= textbox_Enter;
                        t.MouseUp -= textbox_Enter;
                        t.TextChanged -= desc_TextChanged;
                    }

                    foreach ( NumericUpDown n in qtyBox )
                    {
                        n.ValueChanged -= qtyBox_ValueChanged;
                        n.Enter -= numBox_Enter;
                        n.MouseUp -= numBox_Enter;
                        n.Leave -= updown_leave;
                    }

                    toggle_flag = 3;                                        //current mode set
                }
                else
                { }
            }
            else
            {
                if ( s == "fast_load" && toggle_flag == 1 )                 //only the active mode cn be reset
                {
                    billname.Enter += textbox_Enter;
                    billname.Leave += billname_Leave;
                    billname.MouseUp += textbox_Enter;
                    billname.TextChanged += billname_TextChanged;
                    billaddr.Enter += textbox_Enter;
                    billaddr.MouseUp += textbox_Enter;
                    billaddr.TextChanged += billaddr_TextChanged;
                    invdt.ValueChanged += datetime_TextChanged;
                    ddt.ValueChanged += datetime_TextChanged;

                    foreach ( TextBox t in descBox )
                    {
                        t.Leave += descBox_LostFocus;
                        t.Enter += textbox_Enter;
                        t.MouseUp += textbox_Enter;
                        t.TextChanged += desc_TextChanged;
                    }

                    foreach ( NumericUpDown n in qtyBox )
                    {
                        n.ValueChanged += qtyBox_ValueChanged;
                        n.Enter += numBox_Enter;
                        n.MouseUp += numBox_Enter;
                        n.Leave += updown_leave;
                    }

                    foreach ( NumericUpDown n in uBox )
                    {
                        n.ValueChanged += uBox_ValueChanged;
                        n.Enter += numBox_Enter;
                        n.MouseUp += numBox_Enter;
                        n.Leave += updown_leave;
                    }

                    foreach ( ComboBox c in unitBox )
                    {
                        c.SelectedIndexChanged += unitBox_SelectedIndexChanged;
                    }

                    subtotal.TextChanged += disc_calc;
                    discval.ValueChanged += disc_calc;
                    discval.Enter += numBox_Enter;
                    discval.Leave += updown_leave;
                    discval.MouseUp += numBox_Enter;
                    disctype.SelectedIndexChanged += disctype_SelectedIndexChanged;
                    subttllssdisc.TextChanged += txrt_ValueChanged;
                    txrt.ValueChanged += txrt_ValueChanged;
                    txrt.Enter += numBox_Enter;
                    txrt.Leave += updown_leave;
                    txrt.MouseUp += numBox_Enter;
                    paid.ValueChanged += paid_ValueChanged;
                    paid.Enter += numBox_Enter;
                    paid.Leave += updown_leave;
                    paid.MouseUp += numBox_Enter;
                    grssttl.TextChanged += grssttl_TextChanged;
                    datasrc.SelectedIndexChanged += Datasrc_SelectedIndexChanged;
                    search_name_box.TextChanged += textbox_Enter;
                    search_inv_box.TextChanged += textbox_Enter;
                    dtrange.CheckedChanged += Dtrange_CheckedChanged;
                    search_duedt.CheckedChanged += search_duedt_CheckedChanged;

                    toggle_flag = 0;                                        //mode reset
                }
                else if ( s == "history_load" && toggle_flag == 2 )
                {
                    billname.Enter += textbox_Enter;
                    billname.Leave += billname_Leave;
                    billname.MouseUp += textbox_Enter;
                    billname.TextChanged += billname_TextChanged;
                    billaddr.Enter += textbox_Enter;
                    billaddr.MouseUp += textbox_Enter;
                    billaddr.TextChanged += billaddr_TextChanged;

                    foreach ( TextBox t in descBox )
                    {
                        t.Leave += descBox_LostFocus;
                        t.Enter += textbox_Enter;
                        t.MouseUp += textbox_Enter;
                        t.TextChanged += desc_TextChanged;
                    }

                    foreach ( NumericUpDown n in qtyBox )
                    {
                        n.ValueChanged += qtyBox_ValueChanged;
                        n.Enter += numBox_Enter;
                        n.MouseUp += numBox_Enter;
                        n.Leave += updown_leave;
                    }

                    grssttl.TextChanged += grssttl_TextChanged;
                    datasrc.SelectedIndexChanged += Datasrc_SelectedIndexChanged;

                    toggle_flag = 0;                                        //mode reset
                }
                else if ( s == "recursive" && toggle_flag == 3 )
                {
                    foreach ( TextBox t in descBox )
                    {
                        t.Leave += descBox_LostFocus;
                        t.Enter += textbox_Enter;
                        t.MouseUp += textbox_Enter;
                        t.TextChanged += desc_TextChanged;
                    }

                    foreach ( NumericUpDown n in qtyBox )
                    {
                        n.ValueChanged += qtyBox_ValueChanged;
                        n.Enter += numBox_Enter;
                        n.MouseUp += numBox_Enter;
                        n.Leave += updown_leave;
                    }
                    toggle_flag = 0;                                        //mode reset
                }
                else
                { }
            }
        }

        //Set keyboard shortcuts
        private void AgricoveBilling_KeyDown( object sender , KeyEventArgs e )
        {
            if ( e.Control && e.KeyCode == Keys.P )           //Ctrl+P for print
            {
                print_Click ( sender , e );
            }
            if ( e.Control && e.KeyCode == Keys.S )          //Ctrl+S for Save
            {
                Form dlg1 = new Form ();
                dlg1.ShowDialog ();
            }
            if ( e.Control && e.KeyCode == Keys.N )         //Ctrl+N for New Invoice (same as refresh)
            {
                newinv_Click ( sender , e );
            }
            if ( e.KeyCode == Keys.F5 )                   //F5 for refresh
            {
                newinv_Click ( sender , e );
            }
            if ( e.Control && e.KeyCode == Keys.F )       //Ctrl+F for search
            {
                find_Click ( sender , e );
            }
            if ( e.Alt && e.KeyCode == Keys.F4 )          //Alt+F4 for close without saving
            {
                Close ();
            }
        }

        //Disable mouse drag on table
        private void invoice_table_MouseDown( object sender , MouseEventArgs e )
        {
            if ( e.Button == MouseButtons.Left )
            {
                keybd_event ( ( byte ) VK_TAB , 0 , KEYEVENTF_EXTENDEDKEY | 0 , 0 );
                return;
            }
        }

        //load all the labels from app.config
        private void initiate_label()
        {
            label_agri_addr.Text = ConfigurationManager.AppSettings [ "address" ];
            label_agri_url.Text = ConfigurationManager.AppSettings [ "website" ];
            label_agri_email.Text = ConfigurationManager.AppSettings [ "email" ];
            label_ph.Text = ConfigurationManager.AppSettings [ "phone" ];
            label_terms1.Text = ConfigurationManager.AppSettings [ "terms1" ];
            label_terms2.Text = ConfigurationManager.AppSettings [ "terms2" ];
        }

        //Add tab index for tab key navigation
        private void add_tabindex()
        {
            int index = 1;
            foreach ( var desc in descBox )
            {
                desc.TabIndex = index;
                index = index + 4;
            }
            index = 2;
            foreach ( var q in qtyBox )
            {
                q.TabIndex = index;
                index = index + 4;
            }
            index = 3;
            foreach ( var q in unitBox )
            {
                q.TabIndex = index;
                index = index + 4;
            }
            index = 4;
            foreach ( var u in uBox )
            {
                u.TabIndex = index;
                index = index + 4;
            }
            discval.TabIndex = index;
            index++;
            disctype.TabIndex = index;
            index++;
            txrt.TabIndex = index;
            index++;
            paid.TabIndex = index;
            index++;
            billname.TabIndex = index;
            index++;
            billaddr.TabIndex = index;
            index++;
            invdt.TabIndex = index;
            index++;
            ddt.TabIndex = index;
            index++;
            save.TabIndex = index;
            index++;
            print.TabIndex = index;
            index++;
            newinv.TabIndex = index;
            index++;
            search.TabIndex = index;
            index++;
            close.TabIndex = index;
            index++;
            minimise.TabIndex = index;
            index++;
            datasrc.TabIndex = index;
            index++;
            invno_search_label.TabIndex = index;
            index++;
            name_search_label.TabIndex = index;
            index++;
            dtrange.TabIndex = index;
            index++;
            search_duedt.TabIndex = index;
            index++;
            datestart.TabIndex = index;
            index++;
            dateend.TabIndex = index;
            index++;
            search_btn.TabIndex = index;
            index++;
            refresh_search.TabIndex = index;
            index++;
            find_gridview.TabIndex = index;
            index++;
        }

        //invoice table cellpaint event
        private void invoice_table_CellPaint( object sender , TableLayoutCellPaintEventArgs e )
        {
            using ( var b = new SolidBrush ( bgColors [ e.Row , e.Column ] ) )
            {
                e.Graphics.FillRectangle ( b , e.CellBounds );
            }
        }

        //Allows you to drag the window holding on any part of it
        private void AgricoveBilling_MouseDown( object sender , MouseEventArgs e )
        {
            if ( e.Button == MouseButtons.Left )
            {
                ReleaseCapture ();
                SendMessage ( Handle , WM_NCLBUTTONDOWN , HT_CAPTION , 0 );
            }
        }

        //Tags hold the array index of controls
        private void add_tag( TextBox [ ] t )
        {
            int i = 0;
            foreach ( var v in t )
            {
                v.Tag = i.ToString ();
                i = i + 1;
            }
        }
        private void add_tag( Label [ ] t )
        {
            int i = 0;
            foreach ( var v in t )
            {
                v.Tag = i.ToString ();
                i = i + 1;
            }
        }
        private void add_tag( NumericUpDown [ ] t )
        {
            int i = 0;
            foreach ( var v in t )
            {
                v.Tag = i.ToString ();
                i = i + 1;
            }
        }
        private void add_tag( ComboBox [ ] t )
        {
            int i = 0;
            foreach ( var v in t )
            {
                v.Tag = i.ToString ();
                i = i + 1;
            }
        }

        //Add contextmenu to textbox
        private void add_context( TextBox [ ] T )
        {
            foreach ( var v in T )
            {
                v.ContextMenuStrip = copypastemenu;
            }
        }

        //Context Menus
        private void copyToolStripMenuItem_Click( object sender , EventArgs e )
        {
            ToolStripItem item = ( sender as ToolStripItem );
            if ( item != null )
            {
                ContextMenuStrip owner = item.Owner as ContextMenuStrip;
                if ( owner != null )
                {
                    Clipboard.SetText ( owner.SourceControl.Text );
                }
            }
        }

        private void pasteToolStripMenuItem_Click( object sender , EventArgs e )
        {
            ToolStripItem item = ( sender as ToolStripItem );
            if ( item != null )
            {
                ContextMenuStrip owner = item.Owner as ContextMenuStrip;
                if ( owner != null )
                {
                    TextBox sourceControl = owner.SourceControl as TextBox;
                    sourceControl.SelectedText = Clipboard.GetText ();
                }
            }
        }

        private void copyToolStripMenuItem1_Click( object sender , EventArgs e )
        {
            ToolStripItem item = ( sender as ToolStripItem );
            if ( item != null )
            {
                ContextMenuStrip owner = item.Owner as ContextMenuStrip;
                if ( owner != null )
                {
                    DataGridView dg = owner.SourceControl as DataGridView;
                    if ( dg.CurrentCell.RowIndex != 0 )
                    {
                        Clipboard.SetText ( dg.CurrentCell.Value.ToString () );
                    }
                }
            }
        }

        //Move selection away from the button row
        private void find_gridview_DataSourceChanged( object sender , EventArgs e )
        {
            try
            {
                find_gridview.CurrentCell = find_gridview.Rows [ 0 ].Cells [ 1 ];
            }
            catch { }
        }

        //Set and reset action of Enter button
        private void searchbar_Enter( object sender , EventArgs e )
        {
            //MessageBox.Show("0");
            this.AcceptButton = search_btn;
        }

        private void searchbar_Leave( object sender , EventArgs e )
        {
            this.AcceptButton = null;
        }

        //Auto select text upon enter
        private void numBox_Enter( object sender , EventArgs e )
        {
            NumericUpDown num = ( NumericUpDown ) sender;
            num.Select ( 0 , num.Text.Length );
        }

        private void numBox_Enter( object sender , MouseEventArgs e )
        {
            NumericUpDown num = ( NumericUpDown ) sender;
            num.Select ( 0 , num.Text.Length );
        }
        private void textbox_Enter( object sender , EventArgs e )
        {
            TextBox t = ( TextBox ) sender;
            t.SelectAll ();
        }
        private void textbox_Enter( object sender , MouseEventArgs e )
        {
            TextBox t = ( TextBox ) sender;
            t.SelectAll ();
        }

        //custom onpaint for numericupdown by Loathing from StackOverflow
        private void nud_style( NumericUpDown nud )
        {
            //nud.Font = new Font(FontFamily.GenericSansSerif, 20f, FontStyle.Regular);
            bool isSet = false;
            NW nw = null;
            nud.VisibleChanged += delegate
            {
                // NUD children consist of two child windows:
                // 1) TextBox
                // 2) UpDownBase - which uses user draw
                if ( !isSet )
                {
                    foreach ( Control c in nud.Controls )
                    {
                        if ( !( c is TextBox ) )
                        {
                            // prevent flicker
                            typeof ( Control ).InvokeMember ( "DoubleBuffered" , BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic , null , c , new object [ ] { true } );
                            c.Paint += ( sender , e ) =>
                            {
                                var g = e.Graphics;
                                int h = c.Height;
                                int w = c.Width;


                                // cover up the default Up/Down arrows:
                                //g.FillRectangle(SystemBrushes.Control, 3, 3, w - 6, h/2 - 6);
                                //g.FillRectangle(SystemBrushes.Control, 3, h/2 + 3, w - 6, h/2 - 6);

                                // or hide the entire control
                                if ( nud.Enabled )
                                    g.Clear ( nud.BackColor );
                                else
                                    g.Clear ( SystemColors.Control );
                            };

                            nw = new NW ( c.Handle );
                            isSet = true;
                        }
                    }
                }
            };
        }

        //Material theme for DatgridView by Rohan Kumar
        private void datagridview_style( DataGridView d )
        {
            d.BorderStyle = BorderStyle.None;
            d.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb ( 238 , 239 , 249 );
            d.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            d.DefaultCellStyle.SelectionBackColor = Color.FromArgb ( 20 , 25 , 72 );
            d.DefaultCellStyle.SelectionForeColor = Color.WhiteSmoke;
            d.BackgroundColor = Color.White;

            d.EnableHeadersVisualStyles = false;
            d.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            d.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb ( 20 , 25 , 72 );
            d.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            d.RowHeadersVisible = false;
            d.RowTemplate.Height = 35;
            d.DefaultCellStyle.WrapMode = DataGridViewTriState.True;                                         //multi-line
            //d.AllowUserToResizeRows = false;
            d.ColumnHeadersHeight = 35;
            d.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;        //disable resizing of column headers
            d.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font ( "Verdana" , 8F , FontStyle.Bold );
            d.DefaultCellStyle.Font = new System.Drawing.Font ( "Verdana" , 8F , FontStyle.Bold );
            d.EditMode = DataGridViewEditMode.EditOnEnter;
        }

        //format Gridview
        private void gridview_format( DataGridView d , string s )
        {
            try
            {
                if ( s == "invoice" )
                {
                    d.Columns [ 0 ].HeaderText = "";
                    ButtonColumn.Text = "Edit";
                    d.Columns [ 0 ].Width = 60;
                    d.Columns [ 1 ].Visible = true;
                    d.Columns [ 1 ].HeaderText = "Invoice No.";
                    d.Columns [ 1 ].Width = 120;
                    d.Columns [ 2 ].HeaderText = "Invoice Date";
                    d.Columns [ 2 ].Width = 115;
                    d.Columns [ 3 ].HeaderText = "Due Date";
                    d.Columns [ 4 ].HeaderText = "Customer Name";
                    d.Columns [ 4 ].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;                //this column will auto widen according to available space
                    d.Columns [ 5 ].HeaderText = "Due";
                    d.Columns [ 5 ].DefaultCellStyle.Format = "0.00##";
                    d.AllowUserToDeleteRows = false;
                    d.ReadOnly = true;
                    d.Sort ( d.Columns [ 1 ] , ListSortDirection.Descending );         //Order by invoice time (timestamp in invoince no)
                    d.Sort ( d.Columns [ "Due" ] , ListSortDirection.Descending );         //Order by due
                    d.AllowUserToResizeRows = true;                             //this enabled only in invoice view

                    foreach ( DataGridViewRow v in d.Rows )           // Set due rows red and no Due rows green
                    {
                        if ( decimal_parse ( v.Cells [ 5 ].Value.ToString () ) > 0 )
                        {
                            v.Cells [ 1 ].Style.BackColor = Color.FromArgb ( 255 , 82 , 82 );
                            v.Cells [ 1 ].Style.ForeColor = Color.FromArgb ( 0 , 0 , 0 );
                            v.Cells [ 2 ].Style.BackColor = Color.FromArgb ( 255 , 82 , 82 );
                            v.Cells [ 1 ].Style.ForeColor = Color.FromArgb ( 0 , 0 , 0 );
                            v.Cells [ 3 ].Style.BackColor = Color.FromArgb ( 255 , 82 , 82 );
                            v.Cells [ 1 ].Style.ForeColor = Color.FromArgb ( 0 , 0 , 0 );
                            v.Cells [ 4 ].Style.BackColor = Color.FromArgb ( 255 , 82 , 82 );
                            v.Cells [ 1 ].Style.ForeColor = Color.FromArgb ( 0 , 0 , 0 );
                            v.Cells [ 5 ].Style.BackColor = Color.FromArgb ( 255 , 82 , 82 );
                            v.Cells [ 1 ].Style.ForeColor = Color.FromArgb ( 0 , 0 , 0 );
                        }
                        else
                        {
                            v.Cells [ 1 ].Style.BackColor = Color.FromArgb ( 0 , 188 , 212 );
                            v.Cells [ 1 ].Style.ForeColor = Color.FromArgb ( 255 , 255 , 255 );
                            v.Cells [ 2 ].Style.BackColor = Color.FromArgb ( 0 , 188 , 212 );
                            v.Cells [ 2 ].Style.ForeColor = Color.FromArgb ( 255 , 255 , 255 );
                            v.Cells [ 3 ].Style.BackColor = Color.FromArgb ( 0 , 188 , 212 );
                            v.Cells [ 3 ].Style.ForeColor = Color.FromArgb ( 255 , 255 , 255 );
                            v.Cells [ 4 ].Style.BackColor = Color.FromArgb ( 0 , 188 , 212 );
                            v.Cells [ 4 ].Style.ForeColor = Color.FromArgb ( 255 , 255 , 255 );
                            v.Cells [ 5 ].Style.BackColor = Color.FromArgb ( 0 , 188 , 212 );
                            v.Cells [ 5 ].Style.ForeColor = Color.FromArgb ( 255 , 255 , 255 );
                        }
                    }
                }
                else if ( s == "items" )
                {
                    d.Columns [ 0 ].HeaderText = "";
                    ButtonColumn.Text = "Delete";
                    d.Columns [ 0 ].Width = 60;
                    d.Columns [ 1 ].Visible = false;                           //Hide the ItemID column
                    d.Columns [ 2 ].HeaderText = "Item Name";
                    d.Columns [ 2 ].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;                    //this column will auto widen as per available space
                    d.Columns [ 3 ].HeaderText = "Item Price";
                    d.Columns [ 3 ].Width = 100;
                    d.Columns [ 4 ].Visible = false;
                    d.Columns [ 5 ].Width = 80;
                    d.Columns [ 5 ].DefaultCellStyle.Padding = new Padding ( 6 );
                    d.ReadOnly = false;
                    d.AllowUserToDeleteRows = true;
                    d.AllowUserToResizeRows = false;                        //can't allow due to combocolumn size issues
                                                                            //d.DefaultCellStyle.BackColor = Color.FromArgb(0, 0, 0);
                }
            }
            catch { }
        }

        //--------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Various Utility Functions
        //

        //This is the alternate to decimal.Parse that can parse numbers with thousand seperator 
        private decimal decimal_parse( string s )
        {
            decimal value = 0;
            var allowedStyles = ( NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands );
            if ( s.Length == 0 )
            {
                MessageBox.Show ( "Empty String" );
                return 0;
            }
            else if ( Decimal.TryParse ( s , allowedStyles , CultureInfo.GetCultureInfo ( "EN-us" ) , out value ) )        //Using en-us style
            {
                return value;
            }
            else
            {
                MessageBox.Show ( "Not a number" );
                return 0;
            }
        }

        //get papersize by Amr Mausad StackOverFlow
        public static PaperSize GetPaperSize( string Name )               //Deprecated
        {
            PaperSize size1 = null;
            Name = Name.ToUpper ();
            PrinterSettings settings = new PrinterSettings ();
            foreach ( PaperSize size in settings.PaperSizes )
                if ( size.Kind.ToString ().ToUpper () == Name )
                {
                    size1 = size;
                    break;
                }
            return size1;
        }

        //pull all data up by own row when a middle row has become blank: pull i+1 to i, disable i+1 except descbox, disable i+2 descbox. 
        private void recursive_up( int i )
        {
            try
            {                                   //try because what if i+1 doesn't exist?
                if ( descBox [ i + 1 ].Text.Length > 0 )
                {
                    //load data from i+1 to i
                    descBox [ i ].Text = descBox [ i + 1 ].Text;
                    unitBox [ i ].SelectedIndex = unitBox [ i + 1 ].SelectedIndex;
                    qtyBox [ i ].Value = qtyBox [ i + 1 ].Value;                //qtybox must be updated after unitbox so that qty decimal places is correct
                    uBox [ i ].Value = uBox [ i + 1 ].Value;
                    tBox [ i ].Text = tBox [ i + 1 ].Text;

                    //clear i+1
                    descBox [ i + 1 ].Clear ();
                    qtyBox [ i + 1 ].Value = 0;
                    unitBox [ i + 1 ].SelectedIndex = 0;
                    qtyBox [ i ].DecimalPlaces = 3;             //this must be done after unitbox change
                    uBox [ i + 1 ].Value = 0;
                    tBox [ i + 1 ].Text = "0.00";

                    recursive_up ( i + 1 );
                }
                else
                {
                    return;         //has reached the end of list
                }
            }
            catch
            {
                return;           //has reached the very end of table
            }
        }

        //calculate due in runtime
        private decimal calc_due( decimal paid , decimal price1 , decimal qty1 , decimal price2 , decimal qty2 , decimal price3 , decimal qty3 , decimal price4 , decimal qty4 , decimal price5 , decimal qty5 , decimal price6 , decimal qty6 , decimal price7 , decimal qty7 , decimal price8 , decimal qty8 , decimal price9 , decimal qty9 , decimal price10 , decimal qty10 , decimal price11 , decimal qty11 )
        {
            return ( ( price1 * qty1 ) + ( price2 * qty2 ) + ( price3 * qty3 ) + ( price4 * qty4 ) + ( price5 * qty5 ) + ( price6 * qty6 ) + ( price7 * qty7 ) + ( price8 * qty8 ) + ( price9 * qty9 ) + ( price10 * qty10 ) + ( price11 * qty11 ) - paid );
        }

        private void CaptureScreen()                //deprecated
        {
            Graphics myGraphics = this.CreateGraphics ();
            Size s = this.Size;
            memoryImage = new Bitmap ( s.Width , s.Height , myGraphics );
            Graphics memoryGraphics = Graphics.FromImage ( memoryImage );
            memoryGraphics.CopyFromScreen ( this.Location.X , this.Location.Y , 0 , 0 , s );
        }

        private void printDocument1_PrintPage( System.Object sender , System.Drawing.Printing.PrintPageEventArgs e )           //deprecated
        {
            e.Graphics.DrawImage ( memoryImage , 0 , 0 );
        }
        private void bitmap_print()                         //deprecated
        {
            CaptureScreen ();
            //this.Enabled = false;
            PrintDialog pdi = new PrintDialog ();
            pdi.Document = printDocument1;
            if ( pdi.ShowDialog () == DialogResult.OK )
            {
                printDocument1.Print ();
            }
            else
            {
                MessageBox.Show ( "                Print Cancelled         " );
            }
        }

        //--------------------------------------------------------------------------------------------------------------------------------------------------------------
        //Data fetch/save events
        //

        // Auto Load address 
        private void billname_Leave( object sender , EventArgs e )
        {
            using ( var dataContext = new DBConnection () )
            {
                var invoice = dataContext.Invoice.ToList ();
                var data = invoice.Where ( x => x.BillToName == billname.Text.ToString () )
                                .Select ( x => x.BillToAdd ).FirstOrDefault ();
                if ( data != null )
                {
                    billaddr.Text = data.ToString ();
                }
            }
        }

        private bool inv_exists()
        {
            using ( var dataContext = new DBConnection () )
            {
                var invoices = dataContext.Invoice.ToList ();
                var p = from x in invoices where x.InvoiceNo == invno.Text select x.InvoiceNo;
                var result = p.FirstOrDefault ();
                if ( result != null )
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        //Fetch price of item. Null if item not found. Hence "decimal?"
        private decimal? fetch_price( string s, List<Items> items )
        {
                var p = from x in items where x.ItemName == s select ( decimal? ) x.ItemPrice;
                decimal? result = p.FirstOrDefault ();
                return result;
        }

        //fetch unit type of item. returns null if none found
        private string fetch_itemtype( string s , List<Items> items )
        {
                var p = from x in items where x.ItemName == s select ( string ) x.ItemUnit;
                string result = p.FirstOrDefault ();
                return result;
        }

        //save to item table
        private void save_item()
        {
            using ( var dataContext = new DBConnection () )
            {
                var items = dataContext.Items.ToList ();
                int i = 0;                              //array index identifier
                foreach ( var v in descBox )                      //iterate between all items in invoice
                {
                    if ( v.Text.Length > 0 )
                    {
                        var item = ( from x in items
                                     where x.ItemName == v.Text
                                     select x ).FirstOrDefault ();             //Firstordefault to ensure we get null if nothing is found
                        if ( item != null )                                   //If item is present then only update the price and unit
                        {
                            if ( !inv_exists () )
                            {
                                item.ItemPrice = uBox [ i ].Value;
                                item.ItemUnit = unitBox [ i ].Text;
                            }
                        }
                        else                                                //else save as a new entry
                        {
                            Items item2 = new Items ();
                            item2.ItemName = v.Text.ToString ();
                            item2.ItemPrice = uBox [ i ].Value;
                            item2.ItemUnit = unitBox [ i ].Text;
                            dataContext.Items.Add ( item2 );
                        }
                    }
                    i = i + 1;                                      //table array index increased
                }
                try
                {
                    dataContext.SaveChanges ();
                }
                catch
                {
                    MessageBox.Show ( "       Database write permission not available.     " );                //Windows file permission issue
                }
            }
        }

        //Save invoice
        private void save_invoice()
        {
            using ( var dataContext = new DBConnection () )
            {
                var invoices = dataContext.Invoice.ToList ();
                var invoice = ( from x in invoices where x.InvoiceNo == invno.Text select x ).FirstOrDefault ();               //check whether invoice ID already exists
                if ( invoice != null )
                {
                    dataContext.Invoice.Remove ( invoice );                            //remove existing invoice
                }
                dataContext.Invoice.Add ( new Invoice ()
                {
                    InvoiceNo = invno.Text ,
                    InvoiceDt = invdt.Text ,
                    DueDt = ddt.Text ,
                    BillToName = billname.Text ,
                    BillToAdd = billaddr.Text ,
                    ItemID1Name = descBox1.Text ,
                    ItemID1Price = uBox1.Value ,
                    ItemID1Qty = qtyBox1.Value ,
                    ItemID1Unit = unitBox1.Text ,
                    ItemID2Name = descBox2.Text ,
                    ItemID2Price = uBox2.Value ,
                    ItemID2Qty = qtyBox2.Value ,
                    ItemID2Unit = unitBox2.Text ,
                    ItemID3Name = descBox3.Text ,
                    ItemID3Price = uBox3.Value ,
                    ItemID3Qty = qtyBox3.Value ,
                    ItemID3Unit = unitBox3.Text ,
                    ItemID4Name = descBox4.Text ,
                    ItemID4Price = uBox4.Value ,
                    ItemID4Qty = qtyBox4.Value ,
                    ItemID4Unit = unitBox5.Text ,
                    ItemID5Name = descBox5.Text ,
                    ItemID5Price = uBox5.Value ,
                    ItemID5Qty = qtyBox5.Value ,
                    ItemID5Unit = unitBox5.Text ,
                    ItemID6Name = descBox6.Text ,
                    ItemID6Price = uBox6.Value ,
                    ItemID6Qty = qtyBox6.Value ,
                    ItemID6Unit = unitBox6.Text ,
                    ItemID7Name = descBox7.Text ,
                    ItemID7Price = uBox7.Value ,
                    ItemID7Qty = qtyBox7.Value ,
                    ItemID7Unit = unitBox7.Text ,
                    ItemID8Name = descBox8.Text ,
                    ItemID8Price = uBox8.Value ,
                    ItemID8Qty = qtyBox8.Value ,
                    ItemID8Unit = unitBox8.Text ,
                    ItemID9Name = descBox9.Text ,
                    ItemID9Price = uBox9.Value ,
                    ItemID9Qty = qtyBox9.Value ,
                    ItemID9Unit = unitBox9.Text ,
                    ItemID10Name = descBox10.Text ,
                    ItemID10Price = uBox10.Value ,
                    ItemID10Qty = qtyBox10.Value ,
                    ItemID10Unit = unitBox10.Text ,
                    ItemID11Name = descBox11.Text ,
                    ItemID11Price = uBox11.Value ,
                    ItemID11Qty = qtyBox11.Value ,
                    ItemID11Unit = unitBox11.Text ,
                    DiscountValue = discval.Value ,
                    DiscountType = disctype.SelectedIndex ,
                    TaxRate = txrt.Value ,
                    Paid = paid.Value
                    //Due = decimal_parse(balancedue.Text.ToString())                   //due not save because it is calculated dynamically
                } );
                ;
                try
                {
                    dataContext.SaveChanges ();
                }
                catch
                {
                    MessageBox.Show ( "       Database write permission not available.     " );
                }
            }
        }

        //Load data for print
        private reporter load_crystalreports()
        {
            reporter r = new reporter (
                invno.Text ,
                invdt.Text ,
                ddt.Text ,
                billname.Text ,
                billaddr.Text ,
                descBox1.Text ,
                uBox1.Value ,
                qtyBox1.Value ,
                unitBox1.Text ,
                decimal_parse ( tBox1.Text ) ,
                descBox2.Text ,
                uBox2.Value ,
                qtyBox2.Value ,
                unitBox2.Text ,
                decimal_parse ( tBox2.Text ) ,
                descBox3.Text ,
                uBox3.Value ,
                qtyBox3.Value ,
                unitBox3.Text ,
                decimal_parse ( tBox3.Text ) ,
                descBox4.Text ,
                uBox4.Value ,
                qtyBox4.Value ,
                unitBox4.Text ,
                decimal_parse ( tBox4.Text ) ,
                descBox5.Text ,
                uBox5.Value ,
                qtyBox5.Value ,
                unitBox5.Text ,
                decimal_parse ( tBox5.Text ) ,
                descBox6.Text ,
                uBox6.Value ,
                qtyBox6.Value ,
                unitBox6.Text ,
                decimal_parse ( tBox6.Text ) ,
                descBox7.Text ,
                uBox7.Value ,
                qtyBox7.Value ,
                unitBox7.Text ,
                decimal_parse ( tBox7.Text ) ,
                descBox8.Text ,
                uBox8.Value ,
                qtyBox8.Value ,
                unitBox8.Text ,
                decimal_parse ( tBox8.Text ) ,
                descBox9.Text ,
                uBox9.Value ,
                qtyBox9.Value ,
                unitBox9.Text ,
                decimal_parse ( tBox9.Text ) ,
                descBox10.Text ,
                uBox10.Value ,
                qtyBox10.Value ,
                unitBox10.Text ,
                decimal_parse ( tBox10.Text ) ,
                descBox11.Text ,
                uBox11.Value ,
                qtyBox11.Value ,
                unitBox11.Text ,
                decimal_parse ( tBox11.Text ) ,
                decimal_parse ( subtotal.Text ) ,
                disctype.Text ,
                discval.Value ,
                decimal_parse ( subttllssdisc.Text ) ,
                txrt.Value ,
                decimal_parse ( ttltax.Text ) ,
                decimal_parse ( grssttl.Text ) ,
                paid.Value ,
                decimal_parse ( balancedue.Text )
                );
            return r;
        }

        //Load data to invoice editor
        private void load_data( Invoice inv )
        {
            invno.Text = inv.InvoiceNo;
            invdt.Value = DateTime.ParseExact ( inv.InvoiceDt , "dd/MM/yyyy" , null );       //Indian format 
            ddt.Value = DateTime.ParseExact ( inv.DueDt , "dd/MM/yyyy" , null );             //Indian format
            billname.Text = inv.BillToName;
            billaddr.Text = inv.BillToAdd;
            descBox1.Text = inv.ItemID1Name;
            qtyBox1.Value = inv.ItemID1Qty;
            unitBox1.Text = inv.ItemID1Unit;
            uBox1.Value = inv.ItemID1Price;                                                  // ordering matters
            descBox2.Text = inv.ItemID2Name;
            qtyBox2.Value = inv.ItemID2Qty;
            unitBox2.Text = inv.ItemID2Unit;
            uBox2.Value = inv.ItemID2Price;
            descBox3.Text = inv.ItemID3Name;
            qtyBox3.Value = inv.ItemID3Qty;
            unitBox3.Text = inv.ItemID3Unit;
            uBox3.Value = inv.ItemID3Price;
            descBox4.Text = inv.ItemID4Name;
            qtyBox4.Value = inv.ItemID4Qty;
            unitBox4.Text = inv.ItemID4Unit;
            uBox4.Value = inv.ItemID4Price;
            descBox5.Text = inv.ItemID5Name;
            qtyBox5.Value = inv.ItemID5Qty;
            unitBox5.Text = inv.ItemID5Unit;
            uBox5.Value = inv.ItemID5Price;
            descBox6.Text = inv.ItemID6Name;
            qtyBox6.Value = inv.ItemID6Qty;
            unitBox6.Text = inv.ItemID6Unit;
            uBox6.Value = inv.ItemID6Price;
            descBox7.Text = inv.ItemID7Name;
            qtyBox7.Value = inv.ItemID7Qty;
            unitBox7.Text = inv.ItemID7Unit;
            uBox7.Value = inv.ItemID7Price;
            descBox8.Text = inv.ItemID8Name;
            qtyBox8.Value = inv.ItemID8Qty;
            unitBox8.Text = inv.ItemID8Unit;
            uBox8.Value = inv.ItemID8Price;
            descBox9.Text = inv.ItemID9Name;
            qtyBox9.Value = inv.ItemID9Qty;
            unitBox9.Text = inv.ItemID9Unit;
            uBox9.Value = inv.ItemID9Price;
            descBox10.Text = inv.ItemID10Name;
            qtyBox10.Value = inv.ItemID10Qty;
            unitBox10.Text = inv.ItemID10Unit;
            uBox10.Value = inv.ItemID10Price;
            descBox11.Text = inv.ItemID11Name;
            qtyBox11.Value = inv.ItemID11Qty;
            unitBox11.Text = inv.ItemID11Unit;
            uBox11.Value = inv.ItemID11Price;

            disable_rows ();                        //this enables all the rows that have data

            disctype.SelectedIndex = inv.DiscountType;
            discval.Value = inv.DiscountValue;
            txrt.Value = inv.TaxRate;
            paid.Value = inv.Paid;
            paid_ValueChanged ();               //If paid remains same between two consecurively loaded invoices, due will not be changed. So trigger it manually. Hopefully this wouldn't run concurrently while gross total is still being filled
        }

        //Default gridview load before search
        private void gridview_load( DataGridView d , string s )
        {
            using ( var dataContext = new DBConnection () )
            {
                if ( s == "invoice" )
                {
                    var invoice = dataContext.Invoice.ToList ();

                    var data = invoice
                                //.Where( x => x.Due > 0 )
                                .Select ( x => new
                                {
                                    x.InvoiceNo ,
                                    x.InvoiceDt ,
                                    x.DueDt ,
                                    x.BillToName ,
                                    due = calc_due ( x.Paid , x.ItemID1Price , x.ItemID1Qty , x.ItemID2Price , x.ItemID2Qty , x.ItemID3Price , x.ItemID3Qty , x.ItemID4Price , x.ItemID4Qty , x.ItemID5Price , x.ItemID5Qty , x.ItemID6Price , x.ItemID6Qty , x.ItemID7Price , x.ItemID7Qty , x.ItemID8Price , x.ItemID8Qty , x.ItemID9Price , x.ItemID9Qty , x.ItemID10Price , x.ItemID10Qty , x.ItemID11Price , x.ItemID11Qty )
                                } ).ToList ();

                    //Move everything to a sortable list for easy gridview header click
                    List<invoice_grid_data> datalist = data.Select ( p => new invoice_grid_data () { InvoiceNo = p.InvoiceNo , InvoiceDt = p.InvoiceDt , BillToName = p.BillToName , DueDt = p.DueDt , due = p.due } ).ToList ();
                    SortableBindingList<invoice_grid_data> sorteddata = new SortableBindingList<invoice_grid_data> ( datalist );
                    d.Columns.Clear ();
                    d.Columns.Add ( ButtonColumn );            //need to manually add it here because putting combocolumn at the end was causing issues
                    d.DataSource = sorteddata;
                    gridview_format ( d , "invoice" );
                }
                else if ( s == "items" )
                {
                    var items = dataContext.Items.ToList ();
                    var data = items.Select ( x => new { x.ItemID , x.ItemName , x.ItemPrice , x.ItemUnit } ).ToList ();

                    //Move everything to a sortable list for easy gridview header click
                    List<items_grid_data> datalist = data.Select ( p => new items_grid_data () { ItemID = p.ItemID , ItemName = p.ItemName , ItemPrice = p.ItemPrice , ItemUnit = p.ItemUnit } ).ToList ();
                    SortableBindingList<items_grid_data> sorteddata = new SortableBindingList<items_grid_data> ( datalist );

                    ComboColumn.DataPropertyName = "ItemUnit";       //DataPropertyName is the name of the column in gridview that combobox is bound to
                    d.Columns.Clear ();        //if column already exists remove it anyway and add it again to ensure it is the last column. nothing else worked other than a total clear()
                    d.Columns.Add ( ButtonColumn );        //add button first
                    d.DataSource = sorteddata;
                    d.Columns.Add ( ComboColumn );         //add combo at last
                    gridview_format ( d , "items" );
                }
            }
        }

        //load search result in gridview
        private void search_fill( DataGridView d )
        {
            using ( var dataContext = new DBConnection () )
            {
                if ( datasrc.SelectedIndex == 0 )
                {
                    var dts = datestart.Value;
                    var dte = dateend.Value;
                    if ( search_inv_box.Text.Length > 0 )
                    {
                        var var_invno = search_inv_box.Text.ToString ();

                        var invoice = dataContext.Invoice.ToList ();
                        var data = invoice.Where ( x => x.InvoiceNo == var_invno )
                                        .Select ( x => new
                                        {
                                            x.InvoiceNo ,
                                            x.InvoiceDt ,
                                            x.DueDt ,
                                            x.BillToName ,
                                            due = calc_due ( x.Paid , x.ItemID1Price , x.ItemID1Qty , x.ItemID2Price , x.ItemID2Qty , x.ItemID3Price , x.ItemID3Qty , x.ItemID4Price , x.ItemID4Qty , x.ItemID5Price , x.ItemID5Qty , x.ItemID6Price , x.ItemID6Qty , x.ItemID7Price , x.ItemID7Qty , x.ItemID8Price , x.ItemID8Qty , x.ItemID9Price , x.ItemID9Qty , x.ItemID10Price , x.ItemID10Qty , x.ItemID11Price , x.ItemID11Qty )
                                        } ).ToList ();

                        //Move everything to a sortable list for enabling gridview header click
                        List<invoice_grid_data> datalist = data.Select ( p => new invoice_grid_data () { InvoiceNo = p.InvoiceNo , InvoiceDt = p.InvoiceDt , BillToName = p.BillToName , DueDt = p.DueDt , due = p.due } ).ToList ();
                        SortableBindingList<invoice_grid_data> sorteddata = new SortableBindingList<invoice_grid_data> ( datalist );
                        d.DataSource = sorteddata;
                    }
                    else if ( search_name_box.Text.Length > 0 )
                    {
                        var var_name = search_name_box.Text.ToString ();
                        if ( dtrange.Checked )
                        {
                            var invoice = dataContext.Invoice.ToList ();
                            var data = invoice.Where ( x => x.BillToName.ToLower ().Contains ( var_name.ToLower () ) )                                  //moving everything to lowercase for case insensitive search
                                            .Where ( x => DateTime.ParseExact ( x.InvoiceDt , "dd/MM/yyyy" , null ) >= dts.AddDays ( -1 ) )                //For some reason subtracting 1 is required to include start date in the search
                                            .Where ( x => DateTime.ParseExact ( x.InvoiceDt , "dd/MM/yyyy" , null ) <= dte )
                                            .Select ( x => new
                                            {
                                                x.InvoiceNo ,
                                                x.InvoiceDt ,
                                                x.DueDt ,
                                                x.BillToName ,
                                                due = calc_due ( x.Paid , x.ItemID1Price , x.ItemID1Qty , x.ItemID2Price , x.ItemID2Qty , x.ItemID3Price , x.ItemID3Qty , x.ItemID4Price , x.ItemID4Qty , x.ItemID5Price , x.ItemID5Qty , x.ItemID6Price , x.ItemID6Qty , x.ItemID7Price , x.ItemID7Qty , x.ItemID8Price , x.ItemID8Qty , x.ItemID9Price , x.ItemID9Qty , x.ItemID10Price , x.ItemID10Qty , x.ItemID11Price , x.ItemID11Qty )
                                            } ).ToList ();

                            //Move everything to a sortable list
                            List<invoice_grid_data> datalist = data.Select ( p => new invoice_grid_data () { InvoiceNo = p.InvoiceNo , InvoiceDt = p.InvoiceDt , BillToName = p.BillToName , DueDt = p.DueDt , due = p.due } ).ToList ();
                            SortableBindingList<invoice_grid_data> sorteddata = new SortableBindingList<invoice_grid_data> ( datalist );
                            d.Columns.Clear ();
                            d.Columns.Add ( ButtonColumn );            //need to manually add it here because putting combocolumn at the end was causing issues
                            d.DataSource = sorteddata;
                        }
                        else if ( search_duedt.Checked )
                        {
                            var invoice = dataContext.Invoice.ToList ();
                            var data = invoice.Where ( x => x.BillToName.ToLower ().Contains ( var_name.ToLower () ) )                      //moving everything to lowercase for case insensitive search
                                            .Where ( x => DateTime.ParseExact ( x.DueDt , "dd/MM/yyyy" , null ) >= dts.AddDays ( -1 ) )                    //For some reason subtracting 1 is required to include start date in the search
                                            .Where ( x => DateTime.ParseExact ( x.DueDt , "dd/MM/yyyy" , null ) <= dte )
                                            .Select ( x => new
                                            {
                                                x.InvoiceNo ,
                                                x.InvoiceDt ,
                                                x.DueDt ,
                                                x.BillToName ,
                                                due = calc_due ( x.Paid , x.ItemID1Price , x.ItemID1Qty , x.ItemID2Price , x.ItemID2Qty , x.ItemID3Price , x.ItemID3Qty , x.ItemID4Price , x.ItemID4Qty , x.ItemID5Price , x.ItemID5Qty , x.ItemID6Price , x.ItemID6Qty , x.ItemID7Price , x.ItemID7Qty , x.ItemID8Price , x.ItemID8Qty , x.ItemID9Price , x.ItemID9Qty , x.ItemID10Price , x.ItemID10Qty , x.ItemID11Price , x.ItemID11Qty )
                                            } ).ToList ();

                            //Move everything to a sortable list
                            List<invoice_grid_data> datalist = data.Select ( p => new invoice_grid_data () { InvoiceNo = p.InvoiceNo , InvoiceDt = p.InvoiceDt , BillToName = p.BillToName , DueDt = p.DueDt , due = p.due } ).ToList ();
                            SortableBindingList<invoice_grid_data> sorteddata = new SortableBindingList<invoice_grid_data> ( datalist );
                            d.Columns.Clear ();
                            d.Columns.Add ( ButtonColumn );            //need to manually add it here because putting combocolumn at the end was causing issues
                            d.DataSource = sorteddata;
                        }
                        else
                        {
                            var invoice = dataContext.Invoice.ToList ();
                            var data = invoice.Where ( x => x.BillToName.ToLower ().Contains ( var_name.ToLower () ) )
                                            .Select ( x => new
                                            {
                                                x.InvoiceNo ,
                                                x.InvoiceDt ,
                                                x.DueDt ,
                                                x.BillToName ,
                                                due = calc_due ( x.Paid , x.ItemID1Price , x.ItemID1Qty , x.ItemID2Price , x.ItemID2Qty , x.ItemID3Price , x.ItemID3Qty , x.ItemID4Price , x.ItemID4Qty , x.ItemID5Price , x.ItemID5Qty , x.ItemID6Price , x.ItemID6Qty , x.ItemID7Price , x.ItemID7Qty , x.ItemID8Price , x.ItemID8Qty , x.ItemID9Price , x.ItemID9Qty , x.ItemID10Price , x.ItemID10Qty , x.ItemID11Price , x.ItemID11Qty )
                                            } ).ToList ();

                            //Move everything to a sortable list
                            List<invoice_grid_data> datalist = data.Select ( p => new invoice_grid_data () { InvoiceNo = p.InvoiceNo , InvoiceDt = p.InvoiceDt , BillToName = p.BillToName , DueDt = p.DueDt , due = p.due } ).ToList ();
                            SortableBindingList<invoice_grid_data> sorteddata = new SortableBindingList<invoice_grid_data> ( datalist );
                            d.Columns.Clear ();
                            d.Columns.Add ( ButtonColumn );            //need to manually add it here because putting combocolumn at the end was causing issues
                            d.DataSource = sorteddata;
                        }
                    }
                    else
                    {
                        if ( dtrange.Checked )
                        {
                            var invoice = dataContext.Invoice.ToList ();
                            var data = invoice
                                        .Where ( x => DateTime.ParseExact ( x.InvoiceDt , "dd/MM/yyyy" , null ) >= dts.AddDays ( -1 ) )                //For some reason subtracting 1 is required to include start date in the search
                                        .Where ( x => DateTime.ParseExact ( x.InvoiceDt , "dd/MM/yyyy" , null ) <= dte )
                                        .Select ( x => new
                                        {
                                            x.InvoiceNo ,
                                            x.InvoiceDt ,
                                            x.DueDt ,
                                            x.BillToName ,
                                            due = calc_due ( x.Paid , x.ItemID1Price , x.ItemID1Qty , x.ItemID2Price , x.ItemID2Qty , x.ItemID3Price , x.ItemID3Qty , x.ItemID4Price , x.ItemID4Qty , x.ItemID5Price , x.ItemID5Qty , x.ItemID6Price , x.ItemID6Qty , x.ItemID7Price , x.ItemID7Qty , x.ItemID8Price , x.ItemID8Qty , x.ItemID9Price , x.ItemID9Qty , x.ItemID10Price , x.ItemID10Qty , x.ItemID11Price , x.ItemID11Qty )
                                        } ).ToList ();

                            //Move everything to a sortable list
                            List<invoice_grid_data> datalist = data.Select ( p => new invoice_grid_data () { InvoiceNo = p.InvoiceNo , InvoiceDt = p.InvoiceDt , BillToName = p.BillToName , DueDt = p.DueDt , due = p.due } ).ToList ();
                            SortableBindingList<invoice_grid_data> sorteddata = new SortableBindingList<invoice_grid_data> ( datalist );
                            d.Columns.Clear ();
                            d.Columns.Add ( ButtonColumn );            //need to manually add it here because putting combocolumn at the end was causing issues
                            d.DataSource = sorteddata;
                        }
                        else
                        {
                            var invoice = dataContext.Invoice.ToList ();
                            var data = invoice.Select ( x => new
                            {
                                x.InvoiceNo ,
                                x.InvoiceDt ,
                                x.DueDt ,
                                x.BillToName ,
                                due = calc_due ( x.Paid , x.ItemID1Price , x.ItemID1Qty , x.ItemID2Price , x.ItemID2Qty , x.ItemID3Price , x.ItemID3Qty , x.ItemID4Price , x.ItemID4Qty , x.ItemID5Price , x.ItemID5Qty , x.ItemID6Price , x.ItemID6Qty , x.ItemID7Price , x.ItemID7Qty , x.ItemID8Price , x.ItemID8Qty , x.ItemID9Price , x.ItemID9Qty , x.ItemID10Price , x.ItemID10Qty , x.ItemID11Price , x.ItemID11Qty )
                            } ).ToList ();

                            //Move everything to a sortable list
                            List<invoice_grid_data> datalist = data.Select ( p => new invoice_grid_data () { InvoiceNo = p.InvoiceNo , InvoiceDt = p.InvoiceDt , BillToName = p.BillToName , DueDt = p.DueDt , due = p.due } ).ToList ();
                            SortableBindingList<invoice_grid_data> sorteddata = new SortableBindingList<invoice_grid_data> ( datalist );
                            d.Columns.Clear ();
                            d.Columns.Add ( ButtonColumn );            //need to manually add it here because putting combocolumn at the end was causing issues
                            d.DataSource = sorteddata;
                        }
                    }
                    gridview_format ( d , "invoice" );
                }
                else
                {
                    var items = dataContext.Items.ToList ();
                    var data = items.Where ( x => x.ItemName.ToLower ().Contains ( search_name_box.Text.ToLower () ) )
                                .Select ( x => new { x.ItemName , x.ItemPrice , x.ItemUnit } ).ToList ();

                    //Move everything to a sortable list
                    List<items_grid_data> datalist = data.Select ( p => new items_grid_data () { ItemName = p.ItemName , ItemPrice = p.ItemPrice , ItemUnit = p.ItemUnit } ).ToList ();
                    SortableBindingList<items_grid_data> sorteddata = new SortableBindingList<items_grid_data> ( datalist );

                    ComboColumn.DataPropertyName = "ItemUnit";       //DataPropertyName is the name of the column in gridview that combobox is bound to
                    d.Columns.Clear ();        //if column already exists remove it anyway and add it again to ensure it is the last column. nothing else worked other than a total clear()
                    d.Columns.Add ( ButtonColumn );        //add button first
                    d.DataSource = sorteddata;
                    d.Columns.Add ( ComboColumn );         //add combo at last
                    gridview_format ( d , "items" );
                }
            }
        }

        // Search result button click by Stack Overflow KyleMit
        private void find_gridview_CellContentClick( object sender , DataGridViewCellEventArgs e )
        {
            var senderGrid = ( DataGridView ) sender;

            if ( senderGrid.Columns [ e.ColumnIndex ] is DataGridViewButtonColumn && e.RowIndex >= 0 )
            {
                //TODO - Button Clicked - Execute Code Here
                if ( datasrc.SelectedIndex == 0 )                     //load invoice from search result
                {
                    using ( var dataContext = new DBConnection () )
                    {
                        var invoice = dataContext.Invoice.ToList ();
                        var inv = ( from x in invoice
                                    where x.InvoiceNo == find_gridview.Rows [ e.RowIndex ].Cells [ 1 ].Value.ToString ()
                                    select x ).FirstOrDefault ();
                        edit_check = 1;                             //this must be called before load_data to prevent edit_check from being fired automatically during load
                        toggle_events ( "history_load" , true );
                        load_data ( inv );
                        disable_rows ();
                        toggle_events ( "history_load" , false );
                        print.Enabled = true;
                        find_Click ( sender , e );
                    }
                }
                else
                {                                                                               //delete item
                    using ( var dataContext = new DBConnection () )
                    {
                        var items = dataContext.Items.ToList ();
                        var item = ( from x in items
                                     where x.ItemID == Convert.ToInt32 ( find_gridview.Rows [ e.RowIndex ].Cells [ 1 ].Value )
                                     select x ).FirstOrDefault ();
                        dataContext.Items.Remove ( item );
                        try
                        {
                            dataContext.SaveChanges ();
                        }
                        catch
                        {
                            MessageBox.Show ( "       Database write permission not available.     " );
                        }
                        search_fill ( find_gridview );
                    }
                }
            }
        }

        //Save items edit
        private void find_gridview_Leave( object sender , EventArgs e )
        {
            DataGridView dgview = new DataGridView ();
            dgview = ( DataGridView ) sender;

            if ( datasrc.SelectedIndex == 1 )
            {
                using ( var dataContext = new DBConnection () )
                {
                    var items = dataContext.Items.ToList ();
                    foreach ( DataGridViewRow dg in dgview.Rows )
                    {
                        var item = ( from x in items
                                     where x.ItemID == Convert.ToInt32 ( dg.Cells [ 1 ].Value )
                                     select x ).FirstOrDefault ();
                        if ( item != null )
                        {
                            item.ItemName = dg.Cells [ 2 ].Value.ToString ();                       //Cells[1] is hidden
                            item.ItemPrice = decimal.Parse ( dg.Cells [ 3 ].Value.ToString () );
                            item.ItemUnit = dg.Cells [ 4 ].Value.ToString ();
                        }
                    }
                    try
                    {
                        dataContext.SaveChanges ();
                    }
                    catch
                    {
                        MessageBox.Show ( "       Database write permission not available.     " );
                    }
                    search_fill ( find_gridview );             //reloads updated results
                }
            }
        }

        //--------------------------------------------------------------------------------------------------------------------------------------------------------------
        //Other events
        //

        //invoice part  -----------------------------------------------------------------------------------------------------------------------------------------
        //-------------------------------------------------------------------------------------------------------------------------------------------------------

        //This enables the corresponding row of the currently entered descbox
        private void descBox_Enter( object sender , EventArgs e )
        {
            int i = Int32.Parse ( ( sender as TextBox ).Tag.ToString () );            //reading the array index from tag
            descBox [ i ].SelectAll ();                                     //select the existing text
        }

        // if descbox contains data, fetch its info from database. also enable the next row
        private void descBox_LostFocus( object sender , EventArgs e )
        {
            int i = Int32.Parse ( ( sender as TextBox ).Tag.ToString () );            //read its array index
            if ( descBox [ i ].Text.Length > 0 )
            {
                if ( !inv_exists () )                           //check if there is any existing entry and load the values only if this is a new invoice
                {
                    using ( var dataContext = new DBConnection () )
                    {
                        var items = dataContext.Items.ToList ();
                        var price = fetch_price ( descBox [ i ].Text , items );
                        var unit = fetch_itemtype ( descBox [ i ].Text , items );
                        if ( price != null && unit != null )
                        {
                            uBox [ i ].Value = ( decimal ) price;                 //load price
                            unitBox [ i ].Text = unit;                            //load type
                        }
                    }
                }
                if ( qtyBox [ i ].Value == 0 )                   //if qty hasn't been manually changed, load default value of 1
                {
                    qtyBox [ i ].Value = 1;
                }
                tBox [ i ].Text = ( qtyBox [ i ].Value * uBox [ i ].Value ).ToString ( "#,##0.00" );          //calculate total value
            }
            else
            {
                //if there is nothing on descbox then disable its row
                qtyBox [ i ].Value = 0;
                unitBox [ i ].SelectedIndex = 0;
                qtyBox [ i ].DecimalPlaces = 3;             //this must be done after unitbox change
                uBox [ i ].Value = 0;

                try
                {
                    if ( descBox [ i + 1 ].Text.Length > 0 )             //if there is more data below
                    {
                        toggle_events ( "recursive" , true );                       //set the recursive flag to prevent this current function from being called again and again
                        recursive_up ( i );                        //raise everything by one row if there are more data below
                        toggle_events ( "recursive" , false );                      //reset the recursive flag to resume normal operation
                    }
                }
                catch { }
            }
            toggle_events ( "fast_load" , true );
            disable_rows ();
            toggle_events ( "fast_load" , false );
        }

        //quantity changed
        private void qtyBox_ValueChanged( object sender , EventArgs e )
        {
            int i = Int32.Parse ( ( sender as NumericUpDown ).Tag.ToString () );
            tBox [ i ].Text = ( qtyBox [ i ].Value * uBox [ i ].Value ).ToString ( "#,##0.00" );          //tostring format to thousand seperator
            numbox_TextChanged ( sender , ( ulong ) ( i + 13 ) );                                    //set the binary 
        }

        //change unit
        private void unitBox_SelectedIndexChanged( object sender , EventArgs e )
        {
            int i = Int32.Parse ( ( sender as ComboBox ).Tag.ToString () );
            if ( unitBox [ i ].SelectedIndex == 2 )
            {
                qtyBox [ i ].DecimalPlaces = 0;                //pieces can't be in decimal
            }
            else
            {
                qtyBox [ i ].DecimalPlaces = 3;
            }
        }

        //unit value changed
        private void uBox_ValueChanged( object sender , EventArgs e )
        {
            int i = Int32.Parse ( ( sender as NumericUpDown ).Tag.ToString () );
            tBox [ i ].Text = ( qtyBox [ i ].Value * uBox [ i ].Value ).ToString ( "#,##0.00" );
            numbox_TextChanged ( sender , ( ulong ) ( i + 24 ) );
        }

        //recalculate total upon total value updated
        private void tBox_TextChanged( object sender , EventArgs e )
        {
            decimal sum = 0;
            foreach ( var v in tBox )
            {
                sum = sum + decimal_parse ( v.Text.ToString () );
            }
            subtotal.Text = sum.ToString ( "#,##0.00" );
        }

        //discount type changed.
        private void disctype_SelectedIndexChanged( object sender , EventArgs e )
        {
            if ( disctype.SelectedIndex == 0 )
            {
                discval.Value = 0;
                discval.DecimalPlaces = 2;
                discval.Maximum = 99999999;
            }
            else if ( disctype.SelectedIndex == 1 )
            {
                discval.Value = 0;
                discval.DecimalPlaces = 0;
                discval.Maximum = 100;                          //when in percentage, max should be 100%
            }
            disc_calc ( sender , e );
        }

        //calculate discount
        private void disc_calc( object sender , EventArgs e )
        {
            decimal value = 0;
            value = decimal_parse ( subtotal.Text.ToString () );

            if ( disctype.SelectedIndex == 0 )
            {
                subttllssdisc.Text = ( value - discval.Value ).ToString ( "#,##0.00" );
            }
            else if ( disctype.SelectedIndex == 1 )
            {
                subttllssdisc.Text = ( value * ( 1 - discval.Value / 100 ) ).ToString ( "#,##0.00" );
            }

            //enable save button here to account for 100% discount
            value = decimal_parse ( subtotal.Text.ToString () );
            if ( value == 0 )
            {
                save.Enabled = false;
                //print.Enabled = false;
            }
            else
            {
                save.Enabled = true;
                //print.Enabled = true;
            }
            numbox_TextChanged ( sender , 36 );                 //send for edit log
        }

        //calculate total tax
        private void txrt_ValueChanged( object sender , EventArgs e )
        {
            decimal value = 0;
            value = decimal_parse ( subttllssdisc.Text.ToString () );

            ttltax.Text = ( value * txrt.Value / 100 ).ToString ( "#,##0.00" );
            grssttl.Text = ( value * ( 1 + txrt.Value / 100 ) ).ToString ( "#,##0.00" );
            numbox_TextChanged ( sender , 37 );                 //Send for edit log
    }

        //total value changes
        private void grssttl_TextChanged( object sender , EventArgs e )
        {
            decimal value = decimal_parse ( grssttl.Text );
            if ( !paid_token )            //value loads automatically only if user hasn't set it manually
            {
                try
                {
                    paid.Value = value;
                }
                catch
                {
                    MessageBox.Show ( "      Value out of bounds. Are you Bill Gates?    " );
                }
            }
            else if ( paid_max >= value )      //value locked to the max that user had entered
            {
                paid.Value = value;
            }
            else if ( paid_max < value )       //value loads automatically up to the max that user had entered
            {
                paid.Value = paid_max;
            }

            balancedue.Text = ( value - paid.Value ).ToString ( "#,##0.00" );
        }

        //paid value changed
        private void paid_ValueChanged()
        {
            decimal value = 0;
            value = decimal_parse ( grssttl.Text.ToString () );

            if ( paid.Value > value )
            {
                //MessageBox.Show("         You can't pay more than what's due      ");
                paid.Value = value;
            }
            balancedue.Text = ( value - paid.Value ).ToString ( "#,##0.00" );
        }

        //paid value typed in 
        private void paid_ValueChanged( object sender , KeyEventArgs e )
        {
            paid_ValueChanged ();
            paid_token = true;                      //paid_token sets only when value is changed manually
            paid_max = paid.Value;                  //save the max value user has typed
            numbox_TextChanged ( sender , 38 );           //send for edit log
        }

        //when paid value is auto calculated
        private void paid_ValueChanged( object sender , EventArgs e )
        {
            paid_ValueChanged ();
            numbox_TextChanged ( sender , 38 );       //send for edit log
        }

        //don't allow blank in numericupdown
        private void updown_leave( object sender , EventArgs e )
        {
            if ( sender.GetType ().ToString () == "System.Windows.Forms.NumericUpDown" )                    //Necessary to avoid collision with disctype combobox
            {
                NumericUpDown obj = new NumericUpDown ();
                obj = ( NumericUpDown ) sender;
                if ( obj.Text == "" )
                {
                    obj.Value = 0;
                    obj.Text = obj.Value.ToString ();
                }
            }
        }

        //buttons-----------------------------------------------------------------------------------------

        private void close_Click_1( object sender , EventArgs e )
        {
            this.Close ();
        }

        //window minimise button
        private void minimise_Click_1( object sender , EventArgs e )
        {
            WindowState = FormWindowState.Minimized;
        }

        //give user option to print once data has been saved
        private void print_dialogue()
        {
            DialogResult dlgResult = MessageBox.Show ( "invoice has been saved successfully" , "Print" , MessageBoxButtons.YesNo , MessageBoxIcon.Information );

            if ( dlgResult == DialogResult.Yes )
            {
                try
                {
                    Printpreview pv = new Printpreview ( load_crystalreports () );
                    pv.ShowDialog ();                                                   //ShowDialog to make sure user can't interact with parent window
                }
                catch
                {
                    MessageBox.Show ( "         CrystalReports Not Installed      " );
                }
            }
        }

        //save button click
        private void save_Click( object sender , EventArgs e )
        {
            save_item ();
            save_invoice ();
            print_dialogue ();
            newinv_Click ( sender , e );                    //automatically open a new invoice
        }

        //refresh invoice
        private void newinv_Click( object sender , EventArgs e )
        {
            Cursor.Current = Cursors.WaitCursor;        //set cursor to busy until form loads
            toggle_events ( "fast_load" , true );                          //make sure unnecessary events don't get triggered
            cleanform ();
            toggle_events ( "fast_load" , false );
            Cursor.Current = Cursors.Default;
        }

        //Toggle search UI
        private void find_Click( object sender , EventArgs e )
        {
            if ( edit_check < 2 )
            {
                if ( searchpanel.Visible )
                {
                    searchpanel.Visible = false;
                    if ( paid.Value > 0 )           //if invoice is filled
                    {
                        if ( inv_exists () )
                        {
                            print.Enabled = true;
                        }
                        save.Enabled = true;  //It should happen automatically but not if you bo back and forth between result window without loading a new result. Then disc_calc doesn't fire
                    }
                    if ( edit_check == 1 )
                    {
                        search.Text = "Back to Results";
                    }
                    else
                    {
                        search.Text = "&Find";
                    }
                    newinv.Enabled = true;
                    this.AcceptButton = null;
                    find_gridview.DataSource = null;
                    descBox1.Focus ();                           //return default focus
                }
                else
                {
                    searchpanel.Visible = true;
                    save.Enabled = false;
                    print.Enabled = false;
                    search.Text = "Back to Invoice";
                    newinv.Enabled = false;
                    this.AcceptButton = search_btn;
                    search_inv_box.Focus ();

                    if ( datasrc.SelectedItem == null )                    // if loading for the first time
                    {
                        datasrc.SelectedIndex = 0;                     //Necessary to ensure data loads for the first time   
                    }
                    else
                    {
                        search_fill ( find_gridview );                     //load last search                    
                    }
                }
            }
        }

        //print
        private void print_Click( object sender , EventArgs e )
        {
            save_item ();
            save_invoice ();
            try
            {
                Printpreview pv = new Printpreview ( load_crystalreports () );
                pv.ShowDialog ();                                        //ShowDialog to make sure user can't interact with parent window
            }
            catch
            {
                MessageBox.Show ( "         CrystalReports Not Installed      " );
            }
            //newinv_Click ( sender , e );                                    //refresh form once done printing
            //bitmap_print ();                                  //deprecated
        }


        //Search result part---------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------------------------

        private void Dtrange_CheckedChanged( object sender , EventArgs e )
        {
            //toggle
            if ( search_duedt.Checked && dtrange.Checked )
            {
                search_duedt.Checked = false;
            }

            if ( dtrange.Checked )
            {
                datestart.Enabled = true;
                dateend.Enabled = true;
                search_inv_box.Enabled = false;
                search_inv_box.Text = "";
            }
            else
            {
                datestart.Enabled = false;
                dateend.Enabled = false;
                search_inv_box.Enabled = true;
            }
        }

        private void search_duedt_CheckedChanged( object sender , EventArgs e )
        {
            //toggle
            if ( search_duedt.Checked && dtrange.Checked )
            {
                dtrange.Checked = false;
            }

            if ( search_duedt.Checked )
            {
                datestart.Enabled = true;
                dateend.Enabled = true;
                search_inv_box.Enabled = false;
                search_inv_box.Text = "";
            }
            else
            {
                datestart.Enabled = false;
                dateend.Enabled = false;
                search_inv_box.Enabled = true;
            }
        }

        //switch between invoice and items
        private void Datasrc_SelectedIndexChanged( object sender , EventArgs e )
        {
            if ( datasrc.SelectedIndex == 0 )
            {
                name_search_label.Text = "Client Name";
                search_name_box.Width = 262;
                invno_search_label.Visible = true;
                search_inv_box.Visible = true;
                dtrange.Visible = true;
                search_duedt.Visible = true;
                datestart.Visible = true;
                dateend.Visible = true;
                dtrange.Checked = false;
                search_duedt.Checked = false;
                datestart.Enabled = false;
                dateend.Enabled = false;
                gridview_load ( find_gridview , "invoice" );
            }
            else
            {
                name_search_label.Text = "Item Name";
                search_name_box.Width = 540;
                invno_search_label.Visible = false;
                search_inv_box.Visible = false;
                dtrange.Visible = false;
                search_duedt.Visible = false;
                datestart.Visible = false;
                dateend.Visible = false;
                gridview_load ( find_gridview , "items" );
            }
        }

        //Search Button 
        private void Search_btn_Click( object sender , EventArgs e )
        {
            search_fill ( find_gridview );
        }

        //Due date can't be in the past 
        private void datetime_TextChanged( object sender , EventArgs e )
        {
            ddt.MinDate = invdt.Value;              //this will fire even during history load
            if ( invdt.Value.Date > ddt.Value.Date )
            {
                ddt.Value = invdt.Value;
            }
            datetime_Changed ( sender , e );                 //call edit_check
        }

        //refresh button on search results pane
        private void refresh_search_Click( object sender , EventArgs e )
        {
            find_gridview.DataSource = null;                //Necessary to reset user alteration of grid size 
            Datasrc_SelectedIndexChanged ( sender , e );
        }

        //this ensures that combobox selection change on gridview is immediately effective
        private void find_gridview_CurrentCellDirtyStateChanged( object sender , EventArgs e )
        {
            find_gridview.CommitEdit ( DataGridViewDataErrorContexts.Commit );
        }

        //--------------------------------------------------------------------------------------------------------------------------------------------------------------
        // Edit check starts
        //

        // Generate AND key for edit_check
        private ulong andgen( ulong i )
        {
            ulong j;
            ulong ans = 0;
            for ( j = 0 ; j < 64 ; j++ )
            {
                if ( j != i )
                {
                    ans = ans + ( ulong ) Math.Pow ( 2 , j );
                }
            }
            return 0;
        }

        private void textbox_TextChanged( object sender , ulong i )
        {
            TextBox t = new TextBox ();
            t = ( TextBox ) sender;
            if ( edit_check != 1 )
            {
                if ( t.Text.Length == 0 )
                {
                    edit_check = edit_check & andgen ( i );
                }
                else
                {
                    edit_check = edit_check | ( ulong ) Math.Pow ( 2 , i );
                }
            }
            if ( edit_check < 2 )
            {
                search.Enabled = true;
            }
            else
            {
                search.Enabled = false;
            }
        }

        private void numbox_TextChanged( object sender , ulong i )
        {
            if ( sender.GetType ().ToString () == "System.Windows.Forms.NumericUpDown" )                //Necessary to avoid collision with disctype combobox
            {
                NumericUpDown t = new NumericUpDown ();
                t = ( NumericUpDown ) sender;
                if ( edit_check != 1 )
                {
                    if ( t.Value == 0 )
                    {
                        edit_check = edit_check & andgen ( i );
                    }
                    else
                    {
                        edit_check = edit_check | ( ulong ) Math.Pow ( 2 , i );
                    }
                }
                if ( edit_check < 2 )
                {
                    search.Enabled = true;
                }
                else
                {
                    search.Enabled = false;
                }
            }
        }

        private void datetime_Changed( object sender , EventArgs e )
        {
            ulong i = 39;
            if ( edit_check != 1 )
            {
                if ( invdt.Value.Date == ddt.Value.Date )
                {
                    edit_check = edit_check & andgen ( i );
                }
                else
                {
                    edit_check = edit_check | ( ulong ) Math.Pow ( 2 , i );
                }
            }

            if ( edit_check < 2 )
            {
                search.Enabled = true;
            }
            else
            {
                search.Enabled = false;
            }
        }

        private void billname_TextChanged( object sender , EventArgs e )
        {
            textbox_TextChanged ( sender , 1 );
        }

        private void billaddr_TextChanged( object sender , EventArgs e )
        {
            textbox_TextChanged ( sender , 2 );
        }

        private void desc_TextChanged( object sender , EventArgs e )
        {
            TextBox t = new TextBox ();
            t = ( TextBox ) sender;
            textbox_TextChanged ( sender , Convert.ToUInt64 ( t.Tag ) + 2 );
        }
    }
}
