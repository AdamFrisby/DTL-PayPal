/*
 * Copyright (c) DeepThink Pty Ltd, http://www.deepthinklabs.com/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Security.Cryptography;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace DeepThink.PayPal
{
    internal class PayPalTransaction
    {
        internal enum InternalTransactionType
        {
            Payment, // User2User or User2Object "Pay" Option
            Purchase // User2Object "Buy" Option
        }

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

        public readonly InternalTransactionType InternalType;

        // For use only with object purchases.
        public readonly UUID InternalPurchaseFolderID;
        public readonly byte InternalPurchaseType;

        public PayPalTransaction(UUID from, UUID to, string sellersEmail, int amount, Scene scene, string description, InternalTransactionType internalType)
        {
            From = from;
            InternalType = internalType;
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

        public PayPalTransaction(UUID from, UUID to, string sellersEmail, int amount, Scene scene, UUID objectID, string description, InternalTransactionType internalType)
        {
            From = from;
            InternalType = internalType;
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


        public PayPalTransaction(UUID from, UUID to, string sellersEmail, int amount, Scene scene, UUID objectID, string description, InternalTransactionType internalType, UUID folderID, byte saleType)
        {
            From = from;
            InternalType = internalType;
            Description = description;
            Scene = scene;
            Amount = amount;
            SellersEmail = sellersEmail;
            To = to;
            ObjectID = objectID;

            InternalPurchaseFolderID = folderID;
            InternalPurchaseType = saleType;

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