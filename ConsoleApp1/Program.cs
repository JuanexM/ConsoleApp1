using System;
using System.Net;
using System.Net.Sockets;

public class MainClass {
    
    public static void OldMain() {
        Console.WriteLine("[TESTSERVER]");

        //socket
        Socket listenfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //Bind
        IPAddress ipAdr = IPAddress.Parse("127.0.0.1");
        IPEndPoint ipEp = new IPEndPoint(ipAdr, 1234);
        listenfd.Bind(ipEp);

        //Listen
        listenfd.Listen(0);
        Console.WriteLine("[server]start up");
        while (true) {

            //Accept
            Socket connfd = listenfd.Accept();
            Console.WriteLine("[server]accept");

            //Recv
            byte[] readBuff = new byte[1024];
            int count = connfd.Receive(readBuff);
            string str = System.Text.Encoding.UTF8.GetString(readBuff, 0, count);
            Console.WriteLine("[serverAccept]" + str);

            //Send
            if (str == "time") {
                byte[] bytes = System.Text.Encoding.Default.GetBytes(System.DateTime.Now.ToString());
                connfd.Send(bytes);
            }
            else {
                byte[] bytes = System.Text.Encoding.Default.GetBytes("ERROR");
                connfd.Send(bytes);
            }
        }
    }


    public static void Main(string[] arg) {
        Console.WriteLine("【SERVER】");
        Serv serv = new Serv();
        serv.Start("127.0.0.1", 1234);
        while (true) {
            string str = Console.ReadLine();
            switch (str) {
                case ("quit"):return;
            }
        }
    }

}



public class Serv {

    //监听套接字
    public Socket listenfd;

    //客户端连接
    public Conn[] conns;

    //最大连接数
    public int maxConn = 50;

    //获取连接池索引，返回负数表示获取失败
    public int NewIndex() {
        if (conns == null) return -1;
        for(int i = 0; i < conns.Length; i++) {
            if(conns[i] == null) {
                conns[i] = new Conn();
                return i;
            }
            else if(conns[i].isUse == false){
                return i;
            }
        }
        return -1;
    }

    //开启服务器
    public void Start(string host, int port) {
        //连接池
        conns = new Conn[maxConn];
        for(int i = 0; i < maxConn; i++) {
            conns[i] = new Conn();
        }

        //Socket
        listenfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //bind
        IPAddress ipAdr = IPAddress.Parse(host);
        IPEndPoint ipEp = new IPEndPoint(ipAdr, port);
        listenfd.Bind(ipEp);

        //listen
        listenfd.Listen(maxConn);

        //accept
        listenfd.BeginAccept(AcceptCb, null);
        Console.WriteLine("[服务器]启动成功");

    }

    //Accept回调
    private void AcceptCb(IAsyncResult ar) {

        try {
            Socket socket = listenfd.EndAccept(ar);
            int index = NewIndex();
            if(index < 0) {
                socket.Close();
                Console.WriteLine("[警告]连接已满");
            }
            else {
                Conn conn = conns[index];
                conn.Init(socket);
                string adr = conn.GetAdress();
                Console.WriteLine("客户端连接 [" + adr + "] conn 池 ID: " + index);
                conn.socket.BeginReceive(conn.readBuff, conn.buffCount, conn.BuffRemain(), SocketFlags.None, ReceiveCb, conn);
                listenfd.BeginAccept(AcceptCb, null);
            }
        }
        catch(Exception e) {
            Console.WriteLine("AcceptCb失败：" + e.Message);
        }

    }

    //Receive回调
    private void ReceiveCb(IAsyncResult ar) {
        Conn conn = (Conn)ar.AsyncState;
        try {
            int count = conn.socket.EndReceive(ar);

            //关闭信号
            if (count <= 0) {
                Console.WriteLine("收到 [" + conn.GetAdress() + "] 断开连接");
                conn.Close();
                return;
            }

            //数据处理
            string str = System.Text.Encoding.UTF8.GetString(conn.readBuff, 0, count);
            str = conn.GetAdress() + ":" + str;
            byte[] bytes = System.Text.Encoding.Default.GetBytes(str);

            //广播
            for(int i = 0; i < conns.Length; i++) {
                if (conns[i] == null) continue;
                if (!conns[i].isUse) continue;
                Console.WriteLine("将消息转播给" + conns[i].GetAdress());
                conns[i].socket.Send(bytes);
            }

            //继续接收
            conn.socket.BeginReceive(conn.readBuff, conn.buffCount, conn.BuffRemain(), SocketFlags.None, ReceiveCb, conn);
        }
        catch(Exception) {
            Console.WriteLine("收到 [" + conn.GetAdress() + "] 断开连接");
            conn.Close();
        }

    }
}