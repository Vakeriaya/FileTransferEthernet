using System;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace Server
{
    public class User
    {
        private Thread _userThread;
        private string _userName;
        private bool AuthSuccess = false;
        public string Username// вычисляемое свойство
        {
            get { return _userName; }
        }
        private Socket _userHandle;
        public User(Socket handle)
        {
            _userHandle = handle;
            _userThread = new Thread(listner);
            _userThread.IsBackground = true;
            _userThread.Start();
        }

        public int hash(byte[] array)
        {
            int hash = 0;
            for (int i = 0; i < array.Length; i++)
                hash += (array[i] & 0xFF);
            return hash;
        }
        private void listner()
        {
            try
            {
                while (_userHandle.Connected)
                {
                    byte[] buffer = new byte[2048];
                    int bytesReceive = _userHandle.Receive(buffer);
                    handleCommand(Encoding.Unicode.GetString(buffer, 0, bytesReceive));
                }
            }
            catch { Server.EndUser(this); }
        }
        private bool setName(string Name)
        {
            //Тут можно добавить различные проверки
            _userName = Name;
            Server.NewUser(this);
            AuthSuccess = true;
            return true;
        }
        private void handleCommand(string cmd)
        {
            try
            {
                string[] commands = cmd.Split('#');
                int countCommands = commands.Length;
                for (int i = 0; i < countCommands; i++)
                {
                    string currentCommand = commands[i];
                    if (string.IsNullOrEmpty(currentCommand))
                        continue;
                    if (!AuthSuccess)
                    {
                        if (currentCommand.Contains("setname"))
                        {
                            if (setName(currentCommand.Split('|')[1]))
                                Send("#setnamesuccess");
                            else
                                Send("#setnamefailed");
                        }
                        continue;
                    }
                    if(currentCommand.Contains("yy"))
                    {
                        string id = currentCommand.Split('|')[1];
                        Server.FileD file = Server.GetFileByID(int.Parse(id));
                        if (file.ID == 0)
                        {
                            SendMessage("Ошибка при передаче файла...", "1");
                            continue;
                        }
                        Send(file.fileBuffer);
                        Server.Files.Remove(file);
                    }
                    if (currentCommand.Contains("message"))
                    {
                        string[] Arguments = currentCommand.Split('|');
                        Server.SendGlobalMessage($"[{_userName}]: {Arguments[1]}","Black");

                        continue;
                    }//sENDfile
                    //...
                    if(currentCommand.Contains("endsession"))
                    {
                        Server.EndUser(this);
                        return;
                    }
                    if(currentCommand.Contains("sendfileto"))
                    {
                        string[] Arguments = currentCommand.Split('|');
                        string TargetName = Arguments[1];
                        int FileSize = int.Parse(Arguments[2]);
                        string FileName = Arguments[3];
                        byte[] fileBuffer = new byte[FileSize];
                        _userHandle.ReceiveBufferSize = 0xFFFFFFF;
                        _userHandle.SendBufferSize = 0xFFFFFFF;
                        int red = 0;
                        while(red< FileSize)
                        red += _userHandle.Receive(fileBuffer,red, FileSize-red, 0);

                        Console.WriteLine("Hash of file to read- " + hash(fileBuffer)+" "+FileSize+" red|" +red);

                        User targetUser = Server.GetUser(TargetName);
                        if(targetUser == null)
                        {
                            SendMessage($"Пользователь {FileName} не найден!","Black");
                            continue;
                        }
                        Server.FileD newFile = new Server.FileD()
                        {
                            ID = Server.Files.Count+1,
                            FileName = FileName,
                            From = Username,
                            fileBuffer = fileBuffer,
                            FileSize = fileBuffer.Length
                        };
                        Server.Files.Add(newFile);
                        targetUser.SendFile(newFile,targetUser);
                    }
                    if(currentCommand.Contains("private"))
                    {
                        string[] Arguments = currentCommand.Split('|');
                        string TargetName = Arguments[1];
                        string Content = Arguments[2];
                        User targetUser = Server.GetUser(TargetName);
                        if(targetUser == null)
                        {
                            SendMessage($"Пользователь {TargetName} не найден!","Red");
                            continue;
                        }
                        SendMessage($"-[Отправлено][{TargetName}]: {Content}","Black");
                        targetUser.SendMessage($"-[Получено][{Username}]: {Content}","Black");
                        continue;
                    }

                }

            }
            catch (Exception exp) { Console.WriteLine("Error with handleCommand: " + exp.Message); }
        }
        public void SendFile(Server.FileD fd, User To)
        {
            byte[] answerBuffer = new byte[48];
            Console.WriteLine($"Sending {fd.FileName} from {fd.From} to {To.Username}");
            To.Send($"#gfile|{fd.FileName}|{fd.From}|{fd.fileBuffer.Length}|{fd.ID}");
        }
        public void SendMessage(string content,string clr)
        {
            Send($"#msg|{content}|{clr}");
        }
        public void Send(byte[] buffer)
        {
            try
            {
                int red = 0;
                while (red < buffer.Length)
                {
                    red += _userHandle.Send(buffer, red, buffer.Length - red, 0);
                    Console.WriteLine("Trying to "+red);
                }
            }
            catch(Exception ex) {
                Console.WriteLine(ex.StackTrace);
            }
        }
        public void Send(string Buffer)
        {
            try
            {
                _userHandle.Send(Encoding.Unicode.GetBytes(Buffer));
            }
            catch { }
        }
        public void End()
        {
            try
            {
                _userHandle.Close();
            }
            catch { }

        }
    }
}
