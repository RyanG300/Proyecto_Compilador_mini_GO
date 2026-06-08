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
using Mini_GO_compiler.View.UserControls;
using Path = System.IO.Path;

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
        private TextMarkerService markerService;
        private bool run=false;
        
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSyntaxHighlighting();
            SetupErrorMarkers();
        }

        private void SetupErrorMarkers()
        {
            markerService = new TextMarkerService(editor);
            editor.TextArea.TextView.BackgroundRenderers.Add(markerService);
        }

        private void AddErrorMarker(int line, int column, string message)
        {
            try
            {
                var doc = editor.Document;
                if (line < 1 || line > doc.LineCount) return;
                var documentLine = doc.GetLineByNumber(line);
                int start = doc.GetOffset(line, column);
                int length = Math.Max(1, documentLine.EndOffset - start);
                markerService.Create(start, length, message);
            }
            catch (Exception ex)
            {
                // Ignorar errores de offset fuera de rango al agregar el marcador
                Console.WriteLine("Error adding marker: " + ex.Message);
            }
        }

        private void ClearErrorMarkers()
        {
            markerService?.RemoveAll(m => true);
        }

        private void LoadSyntaxHighlighting()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Mini_GO_compiler.MiniGoSyntax.xshd";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    MessageBox.Show("No se encontró el recurso de sintaxis MiniGoSyntax.xshd.");
                    return;
                }

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.XmlResolver = null;

                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    var xshd = HighlightingLoader.LoadXshd(reader);
                    var definition = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
                    HighlightingManager.Instance.RegisterHighlighting("Mini Go", new[] { ".go", ".mgo" }, definition);
                    editor.SyntaxHighlighting = definition;
                }
            }
        }

        private void OpenFolder_click(object sender, RoutedEventArgs e)
        {
            WinForm.FolderBrowserDialog dialog = new WinForm.FolderBrowserDialog();
            WinForm.DialogResult result = dialog.ShowDialog();

            if (result == WinForm.DialogResult.OK)
            {
                currentDirectory = dialog.SelectedPath;
                CreateMainDirectory(sender, e);
                ShowDirectories(sender, e, ArchivosView, currentDirectory, "");
            }
        }

        private void CreateMainDirectory(Object sender, RoutedEventArgs e)
        {
            ArchivosView.Children.Clear();
            Button mainDirectory = new Button();
            if (currentDirectory.Length > 32)
            {
                string[] dirParts = currentDirectory.Split("\\");  
                mainDirectory.Content = "📁 " + dirParts[dirParts.Length - 1];
                Title = dirParts[dirParts.Length - 1]+" - Mini Go Compiler v1";
            }
            else
            {
                mainDirectory.Content = "📁 " + currentDirectory;   
                Title = currentDirectory +" - Mini Go Compiler v1";
            }

            // Aplicar estilo del directorio principal
            mainDirectory.Style = (Style)FindResource("MainDirectoryButtonStyle");

            //-----------contextMenu -----------
            ContextMenu menu = CreateContextMenu(sender, e, currentDirectory,"mainFolder");
            mainDirectory.ContextMenu = menu; 

            ArchivosView.Children.Add(mainDirectory);
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
                dirButton.Content = saltos+"📁 ▸ "+dirParts[dirParts.Length - 1];

                // Aplicar estilo de subdirectorio
                dirButton.Style = (Style)FindResource("DirectoryButtonStyle");

                //-----------contextMenu -----------
                ContextMenu menu = CreateContextMenu(sender, e, dir,"folder");
                dirButton.ContextMenu = menu;

                string dirPath = dir;
                panel.Children.Add(dirButton);
                var abierto = false;
                dirButton.Click += (s, args) =>
                {
                    if (!abierto)
                    {
                        dirButton.Content = saltos+"📂 ▾ "+dirParts[dirParts.Length - 1];
                        ShowDirectories(s, args, panel, dirPath,saltos+"  ");  
                        abierto = true;
                    }
                    else
                    {
                        panel.Children.Clear();
                        dirButton.Content = saltos+"📁 ▸ "+dirParts[dirParts.Length - 1];
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
                //----------Button archivos----------
                Button fileButton = new Button();
                string[] fileParts = file.Split("\\");
                string fileName = fileParts[fileParts.Length - 1];

                // Usar diferentes iconos según la extensión
                string icon = "📄 ";
                if (fileName.EndsWith(".go")) icon = "🔵 ";
                else if (fileName.EndsWith(".txt")) icon = "📝 ";
                else if (fileName.EndsWith(".md")) icon = "📋 ";

                fileButton.Content = saltos+icon+fileName;

                // Aplicar estilo de archivo
                fileButton.Style = (Style)FindResource("FileButtonStyle");

                //------------ContextMenu------------
                ContextMenu menu = CreateContextMenu(sender, e, file,"file");
                fileButton.ContextMenu = menu;
                //Console.WriteLine(context);
                fileButton.Click += (s, args) =>
                {
                    ActualFileName.Text = fileName;
                    ActualFileContainer.Visibility = Visibility.Visible;
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

        private ContextMenu CreateContextMenu(object sender, RoutedEventArgs e, string actualDirectory, string fileOrFolder)
        {
            ContextMenu contextMenu = new ContextMenu();
            var item1 = new MenuItem{Header = "🔨 New file..."};
            item1.Click += (s, args) =>
            {
                CreateFileOrFolder(sender, e, actualDirectory, "file");
            };
            var item2 = new MenuItem { Header = "🔨 New folder..."};
            item2.Click += (s, args) =>
            {
                CreateFileOrFolder(sender, e, actualDirectory, "folder");
            };
            var item3 = new MenuItem { Header = $"✏️ Rename {fileOrFolder}" };
            item3.Click += (s, args) =>
            {
                RenameFileOrFolder(sender, e, actualDirectory, fileOrFolder);
            };
            var item4 = new MenuItem {Header = $"🗑️ Delete {fileOrFolder}"};
            item4.Click += (s, args) =>
            {
                DeleteFileOrFolder(sender, e, actualDirectory, fileOrFolder);
            };
            if (fileOrFolder.Equals("file"))
            {
                contextMenu.Items.Add(item3);
                contextMenu.Items.Add(item4);    
            } else if(fileOrFolder.Equals("mainFolder"))
            {
                contextMenu.Items.Add(item1);
                contextMenu.Items.Add(item2);
            }
            else
            {
                contextMenu.Items.Add(item1);
                contextMenu.Items.Add(item2);
                contextMenu.Items.Add(item3);
                contextMenu.Items.Add(item4); 
            }
            return contextMenu;
        }

        private void RenameFileOrFolder(object sender, RoutedEventArgs e, string actualDirectory, string fileOrFolder)
        {
            InputFileMessageBox inputFileMessageBox = new InputFileMessageBox((fileOrFolder.Equals("file"))? "Insert the new file name":"Insert the new folder name",(fileOrFolder.Equals("file"))? "Rename file name":"Rename folder name",(fileOrFolder.Equals("file")) ? "NewName.txt":"NewName");
            inputFileMessageBox.Owner = this;
            if (inputFileMessageBox.ShowDialog() == true)
            {
                string parentDirectory = System.IO.Path.GetDirectoryName(actualDirectory);
                IEnumerable<string> items = null;
                if (fileOrFolder.Equals("file"))
                {
                    items = Directory.EnumerateFiles(parentDirectory);
                }
                else
                {
                    items = Directory.EnumerateDirectories(parentDirectory);
                }
                foreach (string item in items)
                {
                    string[] itemParts = item.Split("\\");
                    string name = itemParts[itemParts.Length - 1];
                    if (name.Equals(inputFileMessageBox.Answer))
                    {
                        MessageBox.Show((fileOrFolder.Equals("file"))?"File "+inputFileMessageBox.Answer+" already exists in "+actualDirectory:"Folder "+inputFileMessageBox.Answer+" already exists in "+actualDirectory,"Error");
                        return;
                    }
                }

                string[] newNameParts = actualDirectory.Split("\\");
                newNameParts[newNameParts.Length - 1] = inputFileMessageBox.Answer;
                string newName = string.Join("\\", newNameParts);
                if (fileOrFolder.Equals("file"))
                {
                    if (actualDirectory.Equals(currentFile))
                    {
                        currentFile = newName;
                    }
                    File.Move(actualDirectory, newName);
                }
                else
                {
                    Directory.Move(actualDirectory, newName);
                }
                CreateMainDirectory(sender, e);
                ShowDirectories(sender, e, ArchivosView,currentDirectory, "");
                MessageBox.Show((fileOrFolder.Equals("file"))
                    ? "File successfully renamed"
                    : "Folder successfully renamed");
            }
        }

        private void DeleteFileOrFolder(object sender, RoutedEventArgs e, string actualDirectory, string fileOrFolder)
        {
            string[] itemParts = actualDirectory.Split("\\");
            string name = itemParts[itemParts.Length - 1];
            MessageBoxResult result = new MessageBoxResult();
            bool isEmpty = false;
            if (fileOrFolder.Equals("folder"))
            {
                isEmpty = !Directory.EnumerateFileSystemEntries(actualDirectory).Any();
                result = MessageBox.Show((isEmpty)? "Do you really want to delete '"+name+"'?":"Do you really want to delete "+name+" and its content?","Delete",MessageBoxButton.YesNo);
            }
            else
            {
                result = MessageBox.Show("Do you really want to delete '"+name+"'?","Delete",MessageBoxButton.YesNo);
            }

            if (result == MessageBoxResult.Yes)
            {
                if (fileOrFolder.Equals("folder"))
                {
                    if (currentFile != null)
                    {
                        if (System.IO.Path.GetDirectoryName(currentFile).Equals(actualDirectory))
                        {
                            currentFile = null;
                            editor.Text = "";
                            editor.Visibility=Visibility.Collapsed;
                            TextBlock1.Visibility=Visibility.Visible;
                            TextBlock2.Visibility=Visibility.Visible;
                        }    
                    }
                    Directory.Delete(actualDirectory, !isEmpty);
                    MessageBox.Show((isEmpty)?"Folder '"+name+"' successfully deleted":"Folder '"+name+"' successfully deleted along with its content");
                }
                else
                {
                    if (actualDirectory.Equals(currentFile))
                    {
                        currentFile = null;
                        editor.Text = "";
                        editor.Visibility=Visibility.Collapsed;
                        TextBlock1.Visibility=Visibility.Visible;
                        TextBlock2.Visibility=Visibility.Visible;
                        ActualFileContainer.Visibility=Visibility.Hidden;
                    }
                    File.Delete(actualDirectory);
                    MessageBox.Show("File '"+name+"' successfully deleted");
                }
                CreateMainDirectory(sender, e);
                ShowDirectories(sender, e, ArchivosView,currentDirectory, "");
            }
        }
        
        private void CreateFileOrFolder(object sender, RoutedEventArgs e, string actualDirectory, string fileOrFolder)
        {
            InputFileMessageBox inputFileMessageBox = new InputFileMessageBox((fileOrFolder.Equals("file")) ?"Insert file name":"Insert folder name", (fileOrFolder.Equals("file")) ? "File name":"Folder name",(fileOrFolder.Equals("file")) ? "file.txt":"folder");
            inputFileMessageBox.Owner = this;
            if (inputFileMessageBox.ShowDialog() == true)
            {
                IEnumerable<string> items = null;
                if (fileOrFolder.Equals("file"))
                {
                     items = Directory.EnumerateFiles(actualDirectory);
                }
                else
                {
                    items = Directory.EnumerateDirectories(actualDirectory);
                }

                foreach (string item in items)
                {
                    string[] itemParts = item.Split("\\");
                    string name = itemParts[itemParts.Length - 1];
                    if (name.Equals(inputFileMessageBox.Answer))
                    {
                        MessageBox.Show((fileOrFolder.Equals("file"))?"File "+inputFileMessageBox.Answer+" already exists in "+actualDirectory:"Folder "+inputFileMessageBox.Answer+" already exists in "+actualDirectory,"Error");
                        return;
                    }
                }

                if (fileOrFolder.Equals("file"))
                {
                    File.Create(actualDirectory+"\\"+inputFileMessageBox.Answer).Dispose();
                }
                else
                {
                    Directory.CreateDirectory(actualDirectory+"\\"+inputFileMessageBox.Answer);
                }
                CreateMainDirectory(sender, e);
                ShowDirectories(sender, e, ArchivosView,currentDirectory, "");
                MessageBox.Show((fileOrFolder.Equals("file"))? "File "+inputFileMessageBox.Answer+" successfully created":"Folder "+inputFileMessageBox.Answer+" successfully created");
            }
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
                    LinkedList<string> errorList = CompileProcess.PreCompile(context, run);
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
                    if (errorList.Count > 0)
                    {
                        ClearErrorMarkers();
                        foreach (var VARIABLE in errorList)
                        {
                            TextBlock error = new TextBlock();
                            //error.Background = (Brush)bc.ConvertFrom("#454240");
                            error.Foreground = new  SolidColorBrush(Colors.Red);
                            error.FontFamily = new FontFamily("Consolas");
                            error.Margin = new Thickness(8);
                            error.Text = VARIABLE;
                            CompilePanel.Children.Add(error);

                            // Extraer linea y columna para el marcador
                            var match = System.Text.RegularExpressions.Regex.Match(VARIABLE, @"line\s*:\s*(\d+)\s*-\s*column\s*:\s*(\d+)|line\s+(\d+):(\d+)");
                            if (match.Success)
                            {
                                int line = 1, column = 1;
                                if (!string.IsNullOrEmpty(match.Groups[1].Value))
                                {
                                    line = int.Parse(match.Groups[1].Value);
                                    column = int.Parse(match.Groups[2].Value);
                                }
                                else if (!string.IsNullOrEmpty(match.Groups[3].Value))
                                {
                                    line = int.Parse(match.Groups[3].Value);
                                    column = int.Parse(match.Groups[4].Value);
                                }
                                AddErrorMarker(line, column, VARIABLE);
                            }
                        }
                    }
                    else
                    {
                        ClearErrorMarkers();
                        TextBlock text = new TextBlock();
                        text.Foreground = new SolidColorBrush(Colors.White);
                        text.FontFamily = new FontFamily("Consolas");
                        text.Margin = new Thickness(8);
                        text.Text = "Process finished with exit code 0.";
                        CompilePanel.Children.Add(text);
                        if (run)
                        {
                            text.Text = "Output at the terminal (Not at this terminal due to some complication)";
                        }
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

        private void Compile_And_Run_Click(object sender, RoutedEventArgs e)
        {
            run = true;
            Compile_Click(sender, e);
            run = false;
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (currentFile != null)
            {
                string newContent = editor.Text;
                File.WriteAllText(currentFile, newContent);
                CreateMainDirectory(sender, e);
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