using System;
using PCSC;
using PCSC.Exceptions;
using System.Linq;

public class Program
{
    private static readonly IContextFactory _contextFactory = ContextFactory.Instance;

    public static void Main()
    {
        Console.WriteLine("## NTAG215 APDU Demo ##");
        Console.WriteLine("======================");

        try
        {
            var readerName = SelectReader();
            if (string.IsNullOrEmpty(readerName))
            {
                Console.WriteLine("ไม่พบเครื่องอ่าน NFC หรือไม่ได้เลือกเครื่องอ่าน");
                return;
            }

            Console.WriteLine($"ใช้เครื่องอ่าน: {readerName}");
            Console.WriteLine("กรุณาวางการ์ด NTAG215 บนเครื่องอ่าน...");

            using (var context = _contextFactory.Establish(SCardScope.System))
            using (var reader = new SCardReader(context))
            {
                var status = reader.Connect(readerName, SCardShareMode.Shared, SCardProtocol.Any);
                if (status != SCardError.Success)
                {
                    Console.WriteLine("ไม่สามารถเชื่อมต่อกับการ์ดได้ กรุณาวางการ์ดให้ดีแล้วลองใหม่");
                    return;
                }

                // APDU สำหรับ Authentication ด้วยรหัสผ่าน '11223344'
                byte[] authApdu = new byte[] {
                    0xFF, 0x00, 0x00, 0x00,
                    0x07,
                    0xD4, 0x42,
                    0x1B,
                    0x41, 0x42, 0x43, 0x44

                };

                // APDU สำหรับเขียนข้อมูล 'AABBCCDD' ลงในหน้า 77h
                byte[] writeApdu = new byte[] {
                    0xFF, 0x00, 0x00, 0x00,
                    0x08,
                    0xD4, 0x42,
                    0xA2,
                    0x83,
                    0x04, 0x00, 0x00, 0xFF
                };

                // ส่ง Authentication APDU
                Console.WriteLine("\n[1] ส่งคำสั่ง Authentication...");
                var authResp = TransmitApdu(reader, authApdu);
                PrintResponse(authResp);

                // ส่ง Write APDU
                Console.WriteLine("\n[2] ส่งคำสั่ง Write...");
                var writeResp = TransmitApdu(reader, writeApdu);
                PrintResponse(writeResp);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"เกิดข้อผิดพลาด: {ex.Message}");
        }

        Console.WriteLine("\nกด Enter เพื่อปิดโปรแกรม");
        Console.ReadLine();
    }

    private static string? SelectReader()
    {
        using (var context = _contextFactory.Establish(SCardScope.System))
        {
            var readerNames = context.GetReaders();
            if (readerNames == null || readerNames.Length == 0)
            {
                Console.WriteLine("ไม่พบเครื่องอ่าน NFC ที่เชื่อมต่ออยู่");
                return null;
            }

            Console.WriteLine("เครื่องอ่านที่พบ:");
            for (int i = 0; i < readerNames.Length; i++)
            {
                Console.WriteLine($"{i + 1}: {readerNames[i]}");
            }

            Console.Write($"เลือกเครื่องอ่าน (1-{readerNames.Length}): ");
            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= readerNames.Length)
            {
                return readerNames[choice - 1];
            }
            return null;
        }
    }

    private static byte[] TransmitApdu(SCardReader reader, byte[] apdu)
    {
        var sendPci = SCardPCI.GetPci(reader.ActiveProtocol);
        var receivePci = new SCardPCI();
        var receiveBuffer = new byte[258];
        var receiveLength = receiveBuffer.Length;

        var sc = reader.Transmit(
            sendPci,
            apdu,
            apdu.Length,
            receivePci,
            receiveBuffer,
            ref receiveLength
        );

        if (sc != SCardError.Success)
        {
            Console.WriteLine($"Transmit failed: {sc}");
            return Array.Empty<byte>();
        }

        var result = new byte[receiveLength];
        Array.Copy(receiveBuffer, result, receiveLength);
        return result;
    }

    private static void PrintResponse(byte[] response)
    {
        if (response == null || response.Length == 0)
        {
            Console.WriteLine("ไม่มีข้อมูลตอบกลับ");
            return;
        }
        Console.WriteLine($"Response: {BitConverter.ToString(response)}");
        if (response.Length >= 2)
        {
            byte sw1 = response[response.Length - 2];
            byte sw2 = response[response.Length - 1];
            Console.WriteLine($"Status: {sw1:X2} {sw2:X2}");
            if (sw1 == 0x90 && sw2 == 0x00)
            {
                Console.WriteLine("=> Success");
            }
            else
            {
                Console.WriteLine("=> Failed or Warning");
            }
        }
    }
}