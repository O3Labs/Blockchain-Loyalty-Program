using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract
{
    public class OpenPoint : Framework.SmartContract
    {
        //Token Settings
        public static string Name() => "Open Point Token";
        public static string Symbol() => "OPT";
        public static readonly byte[] Owner = "AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y".ToScriptHash();
        public static byte Decimals() => 0;
        private const ulong factor = 100000000; //decided by Decimals()
        private const ulong neo_decimals = 100000000;

        //for the simplicity sake, we don't allow sending NEO and get back tokens.
        //in the future, the contract owner can set the exchange rate and we could allow that.

        public delegate void MyAction<T, T1>(T p0, T1 p1);
        public delegate void MyAction<T, T1, T2>(T p0, T1 p1, T2 p2);

        [DisplayName("transfer")]
        public static event MyAction<byte[], byte[], BigInteger> Transferred;

        public static Object Main(string operation, params object[] args)
        {
            
            if (Runtime.Trigger == TriggerType.Verification)
            {
                if (Owner.Length == 20)
                {
                    // if param Owner is script hash
                    return Runtime.CheckWitness(Owner);
                }
                else if (Owner.Length == 33)
                {
                    // if param Owner is public key
                    byte[] signature = operation.AsByteArray();
                    return VerifySignature(signature, Owner);
                }
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }

                //4 additional methods for loyalty system
                if (operation == "burnTokens")
                {
                    if (args.Length != 2) return 0;
                    byte[] from = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];
                    return BurnFrom(from, value);
                }
                if (operation == "mintTokensTo") 
                {
                    if (args.Length != 2) return 0;
                    byte[] to = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];
                    return MintTokensTo(to, value);
                }
                if (operation == "useTokens")
                {
                    if (args.Length != 2) return 0;
                    byte[] from = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];
                    return UseTokens(from, value);
                }

                if (operation == "totalUsed") return TotalUsed();
                if (operation == "totalBurned") return TotalBurned();

                if (operation == "decimals") return Decimals();
            }


            return false;
        }

        public static object BurnFrom(byte[] from, BigInteger amount)
        {

            if (amount <= 0) return false;

            //this method can only be executed by the owner of the Smart Contract or any admin.
            //this method requires using "sendrawtransaction" so we check the signature here. 
            if (!Runtime.CheckWitness(Owner)) return false;

            //check the balance of the address first
            BigInteger balanceOf = Storage.Get(Storage.CurrentContext, from).AsBigInteger();

            //if the balance if less than a burning amount
            if (balanceOf < amount) {
                return false;
            }

            Storage.Put(Storage.CurrentContext, from, balanceOf - amount); //subtract with the given amount


            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "totalSupply", totalSupply - amount); //subtract with the given amount

            //update totalBurn
            BigInteger totalBurn = Storage.Get(Storage.CurrentContext, "TotalBurned").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "TotalBurned", totalBurn - amount); //subtract with the given amount

            //Runtime.Notify and Event
            return true;
        }

        //Mint tokens to the address
        public static bool MintTokensTo(byte[] to, BigInteger amount)
        {
            if (amount <= 0) return false;

            //this method can only be executed by the owner of the Smart Contract or any admin.
            //this method requires using "sendrawtransaction" so we check the signature here. 
            if (!Runtime.CheckWitness(Owner)) return false;

            //add balance to the address
            BigInteger balance = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, balance + amount);

            //increase the totalSupply
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "totalSupply", amount + totalSupply);
            Transferred(null, to, amount);
            return true;
        }

        // get the total token supply
        // 获取已发行token总量
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }

        // function that is always called when someone wants to transfer tokens.
        // 流转token调用
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
            if (from == to) return true;
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < value) return false;
            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - value);
            BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, to_value + value);
            Transferred(from, to, value);
            return true;
        }

        //Use tokens
        //Subtract from sender
        //Subtract from totalSupply
        //Add to totalUsed
        public static bool UseTokens(byte[] from, BigInteger value)
        {
            if (value <= 0) return false;
            if (!Runtime.CheckWitness(from)) return false;
          
            //subtract the amount from sender
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < value) return false;
            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - value);

            //subtract the totalSupply
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "totalSupply", totalSupply - value); //subtract with the given amount

            //add the totalUsed
            BigInteger totalUsed = Storage.Get(Storage.CurrentContext, "totalUsed").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "totalUsed", totalUsed + value); //add with the given amount
          
            return true;
        }

        public static BigInteger TotalUsed()
        {
            return Storage.Get(Storage.CurrentContext, "totalUsed").AsBigInteger();
        }

        public static BigInteger TotalBurned()
        {
            return Storage.Get(Storage.CurrentContext, "TotalBurned").AsBigInteger();
        }

        // get the account balance of another account with address
        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

    }
}
