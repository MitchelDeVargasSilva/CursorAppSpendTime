using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

public partial class Whip
{
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        RemoveMouseHook();
        UnregisterHotKey(Handle, HOTKEY_ID);
        UnregisterHotKey(Handle, HOTKEY_ID + 1);
        UnregisterHotKey(Handle, HOTKEY_ID + 2);
        UnregisterHotKey(Handle, HOTKEY_ID + 3);
        for (int i = 0; i < WEAPON_COUNT; i++) UnregisterHotKey(Handle, HOTKEY_ID + 10 + i);
        Mixer.Shutdown();
        StopGame();
        RestoreCursor();
        base.OnFormClosed(e);
    }

    static void LogCrash(Exception ex)
    {
        try
        {
            string f = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WhipCursor_erro.log");
            System.IO.File.AppendAllText(f, DateTime.Now + "\r\n" + (ex == null ? "erro desconhecido" : ex.ToString()) + "\r\n\r\n");
            MessageBox.Show("O Whip Cursor encontrou um erro e vai fechar.\nDetalhes salvos em WhipCursor_erro.log (ao lado do exe):\n\n" + (ex == null ? "" : ex.Message), "Whip Cursor");
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        foreach (string a in args)
        {
            string l = a.ToLower();
            if (l == "/restaurar") { RestoreCursorStatic(); return; }
            if (l == "/boot") bootLaunch = true;
        }
        bool created;
        System.Threading.Mutex m = new System.Threading.Mutex(true, "WhipCursorSingleInstance", out created);
        if (!created)
        {
            if (!bootLaunch)
                MessageBox.Show("O Whip Cursor já está aberto (veja o ícone na bandeja, perto do relógio).\nPara reabrir, clique com o botão direito no ícone > Sair, ou encerre WhipCursor.exe no Gerenciador de Tarefas.", "Whip Cursor");
            return;
        }

        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.ThreadException += delegate(object o, System.Threading.ThreadExceptionEventArgs e) { LogCrash(e.Exception); };
        AppDomain.CurrentDomain.UnhandledException += delegate(object o, UnhandledExceptionEventArgs e) { LogCrash(e.ExceptionObject as Exception); };
        try { Application.Run(new Whip()); }
        catch (Exception ex) { LogCrash(ex); try { RestoreCursorStatic(); } catch { } }
        GC.KeepAlive(m);
    }
}
