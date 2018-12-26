using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;


namespace Client
{
    public partial class ChatForm : Form
    {
        private delegate void ChatEvent(string content, string clr);
        private ChatEvent _addMessage;
        private Socket _serverSocket;
        private Thread listenThread;// создает и контролирует поток


        private string _host = Dns.GetHostByName(Dns.GetHostName()).AddressList[0].ToString();//сюда вводим ip хоста с сервером
        private int _port = 8005;// порт сервера

        public int hash(byte[] array)
        {
            int hash = 0;
            for (int i = 0; i < array.Length; i++)
                hash += (array[i] & 0xFF);
            return hash;
        }

        public ChatForm()
        {
            InitializeComponent();
            _addMessage = new ChatEvent(AddMessage);
            userMenu = new ContextMenuStrip();
            ToolStripMenuItem PrivateMessageItem = new ToolStripMenuItem();
            PrivateMessageItem.Text = "Личное сообщение";
            PrivateMessageItem.Click += delegate 
            {
                if (userList.SelectedItems.Count > 0)
                {
                    messageData.Text = $"\"{userList.SelectedItem} ";
                }
            };
            userMenu.Items.Add(PrivateMessageItem);
            ToolStripMenuItem SendFileItem = new ToolStripMenuItem()
            {
                Text = "Отправить файл"
            };
            SendFileItem.Click += delegate 
            {
                if (userList.SelectedItems.Count == 0)
                {
                    return;
                }
                OpenFileDialog ofp = new OpenFileDialog();
                ofp.ShowDialog();
                if (!File.Exists(ofp.FileName))
                {
                    MessageBox.Show($"Файл {ofp.FileName} не найден!");
                    return;
                }
                FileInfo fi = new FileInfo(ofp.FileName);
                byte[] buffer = File.ReadAllBytes(ofp.FileName);
                Console.WriteLine("Hash of file to read- " + hash(buffer));
                Send($"#sendfileto|{userList.SelectedItem}|{buffer.Length}|{fi.Name}");//
                Send(buffer);
            };
            userMenu.Items.Add(SendFileItem);
            userList.ContextMenuStrip = userMenu;

        }

        private void AddMessage(string Content,string Color = "Black")
        {
            if(InvokeRequired)
            {
                Invoke(_addMessage,Content,Color);
                return;
            }
            chatBox.SelectionStart = chatBox.TextLength;
            chatBox.SelectionLength = Content.Length;
            chatBox.SelectionColor = getColor(Color);
            chatBox.AppendText(Content + Environment.NewLine);
        }

        private Color getColor(string text)
        {
            if (Color.Red.Name.Contains(text))//
                return Color.Red;
            return Color.Black;

        }

        private void ChatForm_Load(object sender, EventArgs e)
        {
            IPAddress temp = IPAddress.Parse(_host);
            _serverSocket = new Socket(temp.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
            _serverSocket.Connect(new IPEndPoint(temp, _port));
            if (_serverSocket.Connected)
            {
                enterChat.Enabled = true;
                nicknameData.Enabled = true;
                AddMessage("Связь с сервером установлена.");
                listenThread = new Thread(listner);
                listenThread.IsBackground = true;
                listenThread.Start();
            }
            else
                AddMessage("Связь с сервером не установлена.");
            
        }

        public void Send(byte[] buffer)
        {
            try
            {
                int red=0;
                while(red<buffer.Length)
                    red+=_serverSocket.Send(buffer,red,buffer.Length,0);
            }
            catch { }
        }
        public void Send(string Buffer)
        {
            try
            {
                _serverSocket.Send(Encoding.Unicode.GetBytes(Buffer));
            }
            catch { }
        }


        public void handleCommand(string cmd)
        {
            
                string[] commands = cmd.Split('#');
                int countCommands = commands.Length;
                for (int i = 0; i < countCommands; i++)
                {
                try
                {
                    string currentCommand = commands[i];
                    if (string.IsNullOrEmpty(currentCommand))
                        continue;
                    if (currentCommand.Contains("setnamesuccess"))
                    {
                        
                        
                        //Из-за того что программа пыталась получить доступ к контролам из другого потока вылетал эксепщен и поля не разблокировались

                        Invoke((MethodInvoker)delegate 
                        {
                            AddMessage($"Добро пожаловать, {nicknameData.Text}");
                            nameData.Text = nicknameData.Text;
                            chatBox.Enabled = true;
                            messageData.Enabled = true;
                            userList.Enabled = true;
                            nicknameData.Enabled = false;
                            enterChat.Enabled = false;
                        });
                        continue;
                    }
                    if(currentCommand.Contains("setnamefailed"))
                    {
                        AddMessage("Неверный ник!");
                        continue;
                    }
                    if(currentCommand.Contains("msg"))
                    {
                        string[] Arguments = currentCommand.Split('|');
                        AddMessage(Arguments[1], Arguments[2]);
                        continue;
                    }

                    if(currentCommand.Contains("userlist"))
                    {
                        string[] Users = currentCommand.Split('|')[1].Split(',');
                        int countUsers = Users.Length;
                        userList.Invoke((MethodInvoker)delegate { userList.Items.Clear(); });
                        for(int j = 0;j < countUsers;j++)
                        {
                            userList.Invoke((MethodInvoker)delegate { userList.Items.Add(Users[j]) ; });
                        }
                        continue;

                    }
                    if(currentCommand.Contains("gfile"))
                    {
                        string[] Arguments = currentCommand.Split('|');
                        string fileName = Arguments[1];
                        string FromName = Arguments[2];
                        string FileSize = Arguments[3];
                        string idFile = Arguments[4];
                        DialogResult Result = MessageBox.Show($"Вы хотите принять файл {fileName} размером {FileSize} от {FromName}","Файл",MessageBoxButtons.YesNo);
                        if (Result == DialogResult.Yes)
                        {
                            Thread.Sleep(1000); 
                            Send("#yy|"+idFile);
                            byte[] fileBuffer = new byte[int.Parse(FileSize)];
                            int red = 0;
                            while(red< fileBuffer.Length)
                            {
                                red += _serverSocket.Receive(fileBuffer,red, fileBuffer.Length-red,0);
                                Console.WriteLine("Trying to " + red);

                            }

                            Console.WriteLine("Hash of file- " + hash(fileBuffer)+" redead "+red+" fsize "+ fileBuffer.Length);
                            File.WriteAllBytes(fileName, fileBuffer);
                            MessageBox.Show($"Файл {fileName} принят.");
                        }
                        else
                            Send("nn");
                        continue;
                    }

                }
                catch (Exception exp) { Console.WriteLine("Error with handleCommand: " + exp.Message); }

            }

            
        }
        public void listner()
        {
            try
            {
                while (_serverSocket.Connected)
                {
                    byte[] buffer = new byte[2048];
                    int bytesReceive = _serverSocket.Receive(buffer);
                    handleCommand(Encoding.Unicode.GetString(buffer, 0, bytesReceive));
                }
            }
            catch
            {
                MessageBox.Show("Связь с сервером прервана");
                Application.Exit();
            }
        }

        private void enterChat_Click(object sender, EventArgs e)
        {
            string nickName = nicknameData.Text;
            if (string.IsNullOrEmpty(nickName))
                return;
            Send($"#setname|{nickName}");
        }

        private void ChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_serverSocket.Connected)
                Send("#endsession");
        }

        private void messageData_KeyUp(object sender, KeyEventArgs e)
        {
            if(e.KeyData == Keys.Enter)
            {
                string msgData = messageData.Text;
                if (string.IsNullOrEmpty(msgData))
                    return;
                if(msgData[0] == '"')
                {
                    string temp = msgData.Split(' ')[0];
                    string content = msgData.Substring(temp.Length+1);
                    temp = temp.Replace("\"", string.Empty);
                    Send($"#private|{temp}|{content}");
                }
                else
                    Send($"#message|{msgData}");
                messageData.Text = string.Empty;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.textBox1_openFilePath.Text = openFileDialog.FileName;
                axWindowsMediaPlayer1.URL = textBox1_openFilePath.Text;
            }
        }
    }
}
