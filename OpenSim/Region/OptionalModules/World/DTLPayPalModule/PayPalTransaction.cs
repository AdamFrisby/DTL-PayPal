using System;
using System.Security.Cryptography;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.World.DTLPayPalModule
{
    internal class PayPalTransaction
    {
        public readonly UUID From;
        public readonly UUID To;

        /// <summary>
        /// Email address of seller's account.
        /// </summary>
        public readonly string SellersEmail;

        public readonly string Description;

        /// <summary>
        /// Transaction ID
        /// </summary>
        public readonly UUID TxID;

        /// <summary>
        /// Amount, by default still in cents.
        /// Conversion to Decimal occurs only once,
        /// when generating the PP Invoice.
        /// </summary>
        public readonly int Amount;

        public PayPalTransaction(UUID from, UUID to, string sellersEmail, int amount, Scene scene, string description)
        {
            From = from;
            Description = description;
            Scene = scene;
            Amount = amount;
            SellersEmail = sellersEmail;
            To = to;
            ObjectID = UUID.Zero;

            // Generate a 128-bit Unique ID
            // Using the Crypto Random Generator (increased unguessability)
            byte[] randomBuf = new byte[16];
            RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
            random.GetBytes(randomBuf);
            Guid txID = new Guid(randomBuf);

            TxID = new UUID(txID);
        }

        public PayPalTransaction(UUID from, UUID to, string sellersEmail, int amount, Scene scene, UUID objectID, string description)
        {
            From = from;
            Description = description;
            Scene = scene;
            Amount = amount;
            SellersEmail = sellersEmail;
            To = to;
            ObjectID = objectID;

            // Generate a 128-bit Unique ID
            // Using the Crypto Random Generator (increased unguessability)
            byte[] randomBuf = new byte[16];
            RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
            random.GetBytes(randomBuf);
            Guid txID = new Guid(randomBuf);

            TxID = new UUID(txID);
        }

        public readonly Scene Scene;

        public readonly UUID ObjectID;

        public const string CurrencyCode = "USD";
    }
}