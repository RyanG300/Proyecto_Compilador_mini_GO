using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using WinForm = System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Mini_GO_compiler;

namespace Mini_GO_compiler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string currentDirectory;
        private string currentFile;
        private bool autoSave=false;
        
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSyntaxHighlighting();
        }

        private void LoadSyntaxHighlighting()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Mini_GO_compiler.ide.syntaxHighlighter.xml";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (XmlReader reader = XmlReader.Create(stream))
            {
                var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance); 
                HighlightingManager.Instance.RegisterHighlighting("Mini Go", new[] { ".go" }, definition);
            }
            editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Mini Go");
        }

        private void OpenFolder_click(object sender, RoutedEventArgs e)
        {
            WinForm.FolderBrowserDialog dialog = new WinForm.FolderBrowserDialog();
            WinForm.DialogResult result = dialog.ShowDialog();

            if (result == WinForm.DialogResult.OK)
            {
                currentDirectory = dialog.SelectedPath;
                ArchivosView.Children.Clear();
                TextBlock mainDirectory = new TextBlock();
                if (currentDirectory.Length > 32)
                {
                    string[] dirParts = currentDirectory.Split("\\");  
                    mainDirectory.Text = dirParts[dirParts.Length - 1];
                    Title = dirParts[dirParts.Length - 1]+" - Mini Go Compiler v1";
                }
                else
                {
                    mainDirectory.Text = currentDirectory;   
                    Title = currentDirectory +" - Mini Go Compiler v1";
                }
                mainDirectory.TextAlignment = TextAlignment.Center;
                mainDirectory.Foreground = new SolidColorBrush(Colors.White);
                ArchivosView.Children.Add(mainDirectory);
                ShowDirectories(sender, e, ArchivosView, currentDirectory, "");
            }
        }

        private void ShowDirectories(object sender, RoutedEventArgs e, StackPanel actualPanel, string actualDirectory, string saltos)
        {
            //Directorios
            IEnumerable<String> dirs = Directory.EnumerateDirectories(actualDirectory);
            foreach (string dir in dirs)
            {
                //-----------button directorio-----------
                StackPanel panel = new StackPanel();
                Button dirButton = new Button();
                string[] dirParts = dir.Split("\\"); 
                dirButton.Content = saltos+"📁> "+dirParts[dirParts.Length - 1];
                var bc = new BrushConverter();
                dirButton.Background = (Brush)bc.ConvertFrom("#F5DCD5"); 
                dirButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                
                //-----------contextMenu -----------
                ContextMenu menu = CreateContextMenu(sender, e, dir);
                dirButton.ContextMenu = menu;
                
                string dirPath = dir;
                panel.Children.Add(dirButton);
                var abierto = false;
                dirButton.Click += (s, args) =>
                {
                    if (!abierto)
                    {
                        dirButton.Content = saltos+"📁\\ "+dirParts[dirParts.Length - 1];
                        ShowDirectories(s, args, panel, dirPath,saltos+"-");  
                        abierto = true;
                    }
                    else
                    {
                        panel.Children.Clear();
                        dirButton.Content = saltos+"📁> "+dirParts[dirParts.Length - 1];
                        panel.Children.Add(dirButton);
                        abierto = false;
                    }
                };
                actualPanel.Children.Add(panel);
            }
            
            //Archivos
            IEnumerable<string> files = Directory.EnumerateFiles(actualDirectory);
            foreach (string file in files)
            {
                Button fileButton = new Button();
                string[] fileParts = file.Split("\\");
                fileButton.Content = saltos+"📄"+fileParts[fileParts.Length - 1];
                fileButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                //Console.WriteLine(context);
                fileButton.Click += (s, args) =>
                {
                    string context = File.ReadAllText(file);
                    if (currentFile != null)
                    {
                        if (!File.ReadAllText(currentFile).Equals(editor.Text))
                        {
                            if (autoSave)
                            {
                                Save_Click(sender, e);
                            }
                            else
                            {
                                NotSave_file(sender, e);    
                            }
                        }
                    }
                    currentFile = file;
                    TextBlock1.Visibility = Visibility.Collapsed;
                    editor.Visibility = Visibility.Visible;
                    editor.Text = context;
                    
                    CompilePanel.Children.Clear();
                    var bc = new BrushConverter();
                    //---------Border---------
                    Border border = new Border();
                    border.Background = (Brush)bc.ConvertFrom("#2D2D30");
                    border.CornerRadius = new CornerRadius(3);
                    border.Padding = new Thickness(8, 5, 5, 5);
                    border.Margin = new Thickness(5);
                    
                    //---------TextBlock---------
                    TextBlock errorsNum = new TextBlock();
                    //errorsNum.Background = (Brush)bc.ConvertFrom("#E0D5D3");
                    errorsNum.Foreground = (Brush)bc.ConvertFrom("#4EC9B0");
                    errorsNum.FontSize = 12;
                    errorsNum.FontFamily = new FontFamily("Consolas");
                    errorsNum.Text = "Build errors: 0";
                    border.Child = errorsNum;
                    CompilePanel.Children.Add(border);
                };
                actualPanel.Children.Add(fileButton);
            }
        }

        private ContextMenu CreateContextMenu(object sender, RoutedEventArgs e, string actualDirectory)
        {
            ContextMenu contextMenu = new ContextMenu();
            var item1 = new MenuItem{Header = "🔨 New file..."};
            item1.Click += (s, args) =>
            {
                MessageBox.Show("Prueba 1");
            };
            var item2 = new MenuItem { Header = "🔨 New folder..."};
            item2.Click += (s, args) =>
            {
                MessageBox.Show("Prueba 2");
            };
            var item3 = new MenuItem {Header = "🗑️ Delete folder"};
            item3.Click += (s, args) =>
            {
                string[] dirParts = actualDirectory.Split("//");
                string name = dirParts[dirParts.Length - 1];
                bool isEmpty = !Directory.EnumerateFileSystemEntries(actualDirectory).Any();
                if (isEmpty)
                {
                    MessageBoxResult result = MessageBox.Show("Do you really want to delete '"+name+"'?","Delete",MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        Directory.Delete(actualDirectory);
                        MessageBox.Show("Folder '"+name+"' successfully deleted");
                        ArchivosView.Children.Clear();
                        TextBlock mainDirectory = new TextBlock();
                        mainDirectory.Text = currentDirectory;
                        mainDirectory.TextAlignment = TextAlignment.Center;
                        mainDirectory.Foreground = new SolidColorBrush(Colors.White);
                        ArchivosView.Children.Add(mainDirectory);
                        ShowDirectories(sender, e, ArchivosView,currentDirectory, "");
                    }
                }
                else
                {
                    MessageBoxResult result = MessageBox.Show("Do you really want to delete "+name+" and its content?","Delete",MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        Directory.Delete(actualDirectory, true);
                        MessageBox.Show("Folder '"+name+"' successfully deleted along with its content");
                        ArchivosView.Children.Clear();
                        TextBlock mainDirectory = new TextBlock();
                        mainDirectory.Text = currentDirectory;
                        mainDirectory.TextAlignment = TextAlignment.Center;
                        mainDirectory.Foreground = new SolidColorBrush(Colors.White);
                        ArchivosView.Children.Add(mainDirectory);
                        ShowDirectories(sender, e, ArchivosView,currentDirectory, "");
                    }
                }
            };
            contextMenu.Items.Add(item1);
            contextMenu.Items.Add(item2);
            contextMenu.Items.Add(item3);
            return contextMenu;
        }

        private void NotSave_file(object sender, RoutedEventArgs e)
        {
           MessageBoxResult result = MessageBox.Show("File not saved, would you like to save it?", "Save", MessageBoxButton.YesNo);
           if (result == MessageBoxResult.Yes)
           {
               Save_Click(sender, e);
           }
        }

        private void Auto_Save_Click(object sender, RoutedEventArgs e)
        {
            if (AutoSaveMenuItem.Header.Equals("Auto save: off"))
            {
                AutoSaveMenuItem.Header = "Auto save: on";
                autoSave = true;
            }
            else
            {
                AutoSaveMenuItem.Header = "Auto save: off";
                autoSave = false;
            }
        }

        private void Compile_Click(object sender, RoutedEventArgs e)
        {
            if (currentFile != null)
            {
                string typeFile = System.IO.Path.GetExtension(currentFile).ToLower();
                if (typeFile.Equals(".mgo"))
                {
                    string context = editor.Text;
                    LinkedList<string> errorList = CompileProcess.PreCompile(context);
                    if (errorList.Count > 0)
                    {
                        CompilePanel.Children.Clear();
                        var bc = new BrushConverter();
                        
                        //---------Border---------
                        Border border = new Border();
                        border.Background = (Brush)bc.ConvertFrom("#2D2D30");
                        border.CornerRadius = new CornerRadius(3);
                        border.Padding = new Thickness(8, 5, 5 ,5);
                        border.Margin = new Thickness(5);
                        
                        //---------TextBlock---------
                        TextBlock errorsNum = new TextBlock();
                        //errorsNum.Background = (Brush)bc.ConvertFrom("#E0D5D3");
                        errorsNum.Foreground = (Brush)bc.ConvertFrom("#4EC9B0");
                        errorsNum.FontSize = 12;
                        errorsNum.FontFamily = new FontFamily("Consolas");
                        errorsNum.Text = "Build errors: " + errorList.Count;
                        border.Child = errorsNum;
                        CompilePanel.Children.Add(border);
                        foreach (var VARIABLE in errorList)
                        {
                            TextBlock error = new TextBlock();
                            //error.Background = (Brush)bc.ConvertFrom("#454240");
                            error.Foreground = new  SolidColorBrush(Colors.Red);
                            error.FontFamily = new FontFamily("Consolas");
                            error.Margin = new Thickness(8);
                            error.Text = VARIABLE;
                            CompilePanel.Children.Add(error);
                        }
                    }
                    else
                    {
                        TextBlock text = new TextBlock();
                        text.Foreground = new SolidColorBrush(Colors.White);
                        text.FontFamily = new FontFamily("Consolas");
                        text.Margin = new Thickness(8);
                        text.Text = "Process finished with exit code 0.";
                        CompilePanel.Children.Add(text);
                    }    
                }
                else
                {
                    CompilePanel.Children.Clear();
                    var bc = new BrushConverter();
                    //---------Border---------
                    Border border = new Border();
                    border.Background = (Brush)bc.ConvertFrom("#2D2D30");
                    border.CornerRadius = new CornerRadius(3);
                    border.Padding = new Thickness(8, 5, 5 ,5);
                    border.Margin = new Thickness(5);
                    
                    //---------errorsNum---------
                    TextBlock errorsNum = new TextBlock();
                    //errorsNum.Background = (Brush)bc.ConvertFrom("#E0D5D3");
                    errorsNum.Foreground = (Brush)bc.ConvertFrom("#4EC9B0");;
                    errorsNum.FontSize = 12;
                    errorsNum.FontFamily = new FontFamily("Consolas");
                    errorsNum.Text = "Build errors: " + 1;
                    TextBlock error = new TextBlock();
                    border.Child = error;
                    
                    //---------Error---------
                    //error.Background = (Brush)bc.ConvertFrom("#454240");
                    error.Foreground = new  SolidColorBrush(Colors.Red);
                    error.FontFamily = new FontFamily("Consolas");
                    error.Text = "Unable to compile, the file type is not \".mgo\"";
                    error.Margin = new Thickness(8);
                    CompilePanel.Children.Add(border);
                    CompilePanel.Children.Add(error);
                }
            }
            else
            {
                MessageBox.Show("No file has been selected");   
            }
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (currentFile != null)
            {
                string newContent = editor.Text;
                File.WriteAllText(currentFile, newContent);
                ArchivosView.Children.Clear();
                TextBlock mainDirectory = new TextBlock();
                mainDirectory.Text = currentDirectory;
                mainDirectory.TextAlignment = TextAlignment.Center;
                mainDirectory.Foreground = new SolidColorBrush(Colors.White);
                ArchivosView.Children.Add(mainDirectory);
                ShowDirectories(sender, e, ArchivosView, currentDirectory,"");
                MessageBox.Show("Saved successfully!");
            }
            else
            {
                TextBlock1.Text = "No file has been selected";
            }
        }

        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuItem_About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Mini Go Compiler v1.0");
        }

        private void Button_Hello_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hello World!");
            //TextBlock1.Text = "Hello World2!";
        }
    }
}