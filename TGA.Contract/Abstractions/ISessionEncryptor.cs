namespace TGA.Contract.Abstractions;

public interface ISessionEncryptor
{
    byte[] Encrypt(byte[] raw);
    byte[] Decrypt(byte[] encrypted);
}