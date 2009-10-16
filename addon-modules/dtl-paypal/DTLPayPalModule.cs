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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using Nwc.XmlRpc;

namespace DeepThink.PayPal
{
    public class DTLPayPalModule : ISharedRegionModule, IMoneyModule
    {
        private string m_ppurl = "www.paypal.com"; // Change to www.sandbox.paypal.com for testing.

        private bool m_active;
        private bool m_enabled;

        private readonly object m_setupLock = new object();
        private bool m_setup;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Dictionary<UUID,string> m_usersemail = new Dictionary<UUID, string>();

        private IConfigSource m_config;

        private readonly List<Scene> m_scenes = new List<Scene>();

        private readonly Dictionary<UUID,PayPalTransaction> m_transactionsInProgress = new Dictionary<UUID, PayPalTransaction>();

        #region DTL Currency - PayPal 

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Thanks to Melanie for reminding me about 
        /// EventManager.OnMoneyTransfer being the critical function,
        /// and not ApplyCharge.</remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void EventManager_OnMoneyTransfer(object sender, EventManager.MoneyTransferArgs e)
        {
            if(!m_active)
                return;

            IClientAPI user = null;
            Scene scene = null;

            // Find the user's controlling client.
            lock(m_scenes)
            {
                foreach (Scene sc in m_scenes)
                {
                    List<ScenePresence> avs =
                        sc.GetAvatars().FindAll(
                            x =>
                            (x.UUID == e.sender && x.IsChildAgent == false)
                            );

                    if(avs.Count > 0)
                    {
                        if(avs.Count > 1)
                        {
                            m_log.Warn("[DTL PayPal] Multiple avatars with same UUID! Aborting transaction.");
                            return;
                        }

                        // Found the client,
                        // and their root scene.
                        user = avs[0].ControllingClient;
                        scene = sc;
                    }
                }
            }

            if(scene == null || user == null)
            {
                m_log.Warn("[DTL PayPal] Unable to find scene or user! Aborting transaction.");
                return;
            }

            PayPalTransaction txn;

            if (e.transactiontype == 5008)
            {
                // Object was paid, find it.
                SceneObjectPart sop = scene.GetSceneObjectPart(e.receiver);
                if (sop == null)
                {
                    m_log.Warn("[DTL PayPal] Unable to find SceneObjectPart that was paid. Aborting transaction.");
                    return;
                }

                txn = new PayPalTransaction(e.sender, sop.OwnerID, m_usersemail[sop.OwnerID], e.amount,
                                            scene, e.receiver, e.description + "T:" + e.transactiontype);
            }
            else
            {
                // Payment to a user.
                txn = new PayPalTransaction(e.sender, e.receiver, m_usersemail[e.receiver], e.amount,
                                            scene, e.description + "T:" + e.transactiontype);
            }

            // Add transaction to queue
            lock (m_transactionsInProgress)
                m_transactionsInProgress.Add(txn.TxID, txn);

            string baseUrl = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;

            user.SendLoadURL("DTL PayPal", txn.ObjectID, txn.To, false, "Confirm payment?",
                             "http://" + baseUrl + "/dtlpp/?txn=" + txn.TxID);
        }

        void TransferSuccess(PayPalTransaction transaction)
        {
            if (transaction.ObjectID == UUID.Zero)
            {
                // User 2 User Transaction
                // Probably should notify them somehow.
            }
            else
            {
                if (OnObjectPaid != null)
                {
                    OnObjectPaid(transaction.ObjectID, transaction.From, transaction.Amount);
                }
            }

            // Cleanup.
            lock (m_transactionsInProgress)
                m_transactionsInProgress.Remove(transaction.TxID);
        }

        // Currently hard coded to $0.01 = OS$1
        static decimal ConvertAmountToCurrency(int amount)
        {
            return amount/(decimal) 100;
        }

        public Hashtable DtlUserPage(Hashtable request)
        {
            UUID txnID = new UUID((string) request["txn"]);

            if(!m_transactionsInProgress.ContainsKey(txnID))
            {
                Hashtable ereply = new Hashtable();

                ereply["int_response_code"] = 404; // 200 OK
                ereply["str_response_string"] = "<h1>Invalid Transaction</h1>";
                ereply["content_type"] = "text/html";

                return ereply;
            }

            PayPalTransaction txn = m_transactionsInProgress[txnID];

            string baseUrl = m_scenes[0].RegionInfo.ExternalHostName + ":" + m_scenes[0].RegionInfo.HttpPort;

            // Ouch. (This is the PayPal Request URL)
            string url = "https://" + m_ppurl + "/cgi-bin/webscr?cmd=_xclick" +
                         "&business=" + HttpUtility.UrlEncode(txn.SellersEmail) +
                         "&item_name=" + HttpUtility.UrlEncode(txn.Description) +
                         "&item_number=" + HttpUtility.UrlEncode(txn.TxID.ToString()) +
                         "&amount=" + HttpUtility.UrlEncode(ConvertAmountToCurrency(txn.Amount).ToString()) +
                         "&page_style=" + HttpUtility.UrlEncode("Paypal") +
                         "&no_shipping=" + HttpUtility.UrlEncode("1") +
                         "&return=" + HttpUtility.UrlEncode("http://" + baseUrl + "/") + // TODO: Add in a return page
                         "&cancel_return=" + HttpUtility.UrlEncode("http://" + baseUrl + "/") + // TODO: Add in a cancel page
                         "&notify_url=" + HttpUtility.UrlEncode("http://" + baseUrl + "/dtlppipn") +
                         "&no_note=" + HttpUtility.UrlEncode("1") +
                         "&currency_code=" + HttpUtility.UrlEncode("USD") +
                         "&lc=" + HttpUtility.UrlEncode("US") +
                         "&bn=" + HttpUtility.UrlEncode("PP-BuyNowBF") +
                         "&charset=" + HttpUtility.UrlEncode("UTF-8") +
                         "";


            Hashtable reply = new Hashtable();

            reply["int_response_code"] = 200; // 200 OK
            reply["str_response_string"] = "<h1>PayPal Time</h1><p>Click <a href=\"" + url + "\"> here</a> to continue.";
            reply["content_type"] = "text/html";

            return reply;
        }

        public Hashtable DtlIPN(Hashtable request)
        {
            Hashtable reply = new Hashtable();

            // Does not matter what we send back to PP here.
            reply["int_response_code"] = 200; // 200 OK
            reply["str_response_string"] = "IPN Processed - Have a nice day.";
            reply["content_type"] = "text/html";

            if (!m_active)
            {
                m_log.Error("[DTL PayPal] Recieved IPN request, but module is disabled. Aborting.");
                reply["str_response_string"] = "IPN Not processed. Module is not enabled.";
                return reply;
            }

            Dictionary<string, string> postvals = ServerUtils.ParseQueryString((string) request["body"]);
            string originalPost = (string) request["body"];

            string modifiedPost = originalPost + "&cmd=_notify-validate";

            HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create("https://" + m_ppurl + "/cgi-bin/webscr");
            httpWebRequest.Method = "POST";

            httpWebRequest.ContentLength = modifiedPost.Length;
            StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream());
            streamWriter.Write(modifiedPost);
            streamWriter.Close();

            string response;

            HttpWebResponse httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
            {
                response = streamReader.ReadToEnd();
                streamReader.Close();
            }

            if (httpWebResponse.StatusCode != HttpStatusCode.OK)
            {
                m_log.Error("[DTL PayPal] IPN Status code != 200. Aborting.");
                return reply;
            }

            if (!response.Contains("VERIFIED"))
            {
                m_log.Error("[DTL PayPal] IPN was NOT verified. Aborting.");
                return reply;
            }

            // Handle IPN Components
            try
            {
                if (postvals["payment_status"] != "Confirmed")
                {
                    m_log.Error("[DTL PayPal] Transaction not confirmed. Aborting.");
                    return reply;
                }

                if (postvals["mc_currency"].ToUpper() != "USD")
                {
                    m_log.Error("[DTL PayPal] Payment was made in an incorrect currency (" + postvals["mc_currency"] +
                                "). Aborting.");
                    return reply;
                }

                // Check we have a transaction with the listed ID.
                UUID txnID = new UUID(postvals["item_number"]);
                PayPalTransaction txn;

                lock (m_transactionsInProgress)
                {
                    if (!m_transactionsInProgress.ContainsKey(txnID))
                    {
                        m_log.Error("[DTL PayPal] Recieved IPN request for Payment that is not in progress. Aborting.");
                        return reply;
                    }

                    txn = m_transactionsInProgress[txnID];
                }

                // Check user paid correctly...
                Decimal amountPaid = Decimal.Parse(postvals["mc_gross"]);
                if(ConvertAmountToCurrency(txn.Amount) != amountPaid)
                {
                    m_log.Error("[DTL PayPal] Expected payment was " + ConvertAmountToCurrency(txn.Amount) +
                                " but recieved " + amountPaid + " " + postvals["mc_currency"] + " instead. Aborting.");
                    return reply;
                }

                // At this point, the user has paid, paid a correct amount, in the correct currency.
                // Time to deliver their items. Do it in a seperate thread, so we can return "OK" to PP.
                Util.FireAndForget(delegate { TransferSuccess(txn); });
            }
            catch (KeyNotFoundException)
            {
                m_log.Error("[DTL PayPal] Recieved badly formatted IPN notice. Aborting.");
                return reply;
            }
            // Wheeeee

            return reply;
        }

        #endregion


        #region Implementation of IRegionModuleBase

        public string Name
        {
            get { return "DeepThink PayPal Module - ©2009 DeepThink Pty Ltd."; }
        }

        public Type ReplaceableInterface
        {
            get { return typeof (IMoneyModule); }
        }

        public void Initialise(IConfigSource source)
        {
            m_config = source;
        }

        public void Close()
        {
            m_active = false;
        }

        public void AddRegion(Scene scene)
        {
            lock (m_scenes)
                m_scenes.Add(scene);

            if (m_enabled)
                scene.RegisterModuleInterface<IMoneyModule>(this);

            scene.EventManager.OnMoneyTransfer += EventManager_OnMoneyTransfer;
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_scenes)
                m_scenes.Remove(scene);

            if (m_enabled)
                scene.EventManager.OnMoneyTransfer -= EventManager_OnMoneyTransfer;
        }

        public void RegionLoaded(Scene scene)
        {
            lock (m_setupLock)
                if (m_setup == false)
                {
                    m_setup = true;
                    FirstRegionLoaded();
                }
        }

        public void PostInitialise()
        {
            IConfig config = m_config.Configs["DTL PayPal"];

            if (null == config)
            {
                m_log.Info("[DTL PayPal] No configuration specified. Skipping.");
                return;
            }

            if (config.GetBoolean("Enabled",false))
            {
                m_log.Info("[DTL PayPal] Enabled=true not specified in config. Skipping.");
                return;
            }

            m_ppurl = config.GetString("PayPalURL", m_ppurl);

            if(!config.GetBoolean("Enabled",false))
            {
                m_log.Info("[DTL PayPal] Not enabled.");
                return;
            }

            m_log.Warn("[DTL PayPal] No users specified, skipping load.");


            m_enabled = true;
        }

        public void FirstRegionLoaded()
        {
            IConfig users = m_config.Configs["DTL PayPal Users"];

            if (null == users)
            {
                m_log.Warn("[DTL PayPal] No users specified, skipping load.");
                return;
            }

            CommunicationsManager communicationsManager = m_scenes[0].CommsManager;

            // This aborts at the slightest provocation
            // We realise this may be inconvenient for you,
            // however it is important when dealing with
            // financial matters to error check everything.

            foreach (string user in users.GetKeys())
            {
                m_log.Debug("[DTL PayPal] Looking up UUID for " + user);
                string[] username = user.Split(new[] { ' ' }, 2);
                UserProfileData upd = communicationsManager.UserService.GetUserProfile(username[0], username[1]);

                if (upd != null)
                {

                    m_log.Debug("[DTL PayPal] Found, " + user + " = " + upd.ID);
                    string email = users.GetString(user);

                    if (string.IsNullOrEmpty(email))
                    {
                        m_log.Error("[DTL PayPal] PayPal email address not set for " + user +
                                    " in [DTL PayPal Users] config section. Skipping.");
                        // Did abort here, but since the users are being added to the list regardless...
                    }

                    if (!DTLPayPalHelpers.IsValidEmail(email))
                    {
                        m_log.Error("[DTL PayPal] PayPal email address not valid for " + user +
                                    " in [DTL PayPal Users] config section. Skipping.");
                        // See comment above.
                    }

                    m_usersemail[upd.ID] = email;
                }
                else // UserProfileData was null
                {
                    m_log.Error("[DTL PayPal] Error, User Profile not found for " + user +
                                ". Check the spelling and/or any associated grid services. Aborting.");
                    return;
                }
            }

            // Add HTTP Handlers (user, then PP-IPN)
            MainServer.Instance.AddHTTPHandler("dtlpp", DtlUserPage);
            MainServer.Instance.AddHTTPHandler("dtlppipn", DtlIPN);

            // XMLRPC Handlers for Standalone
            MainServer.Instance.AddXmlRPCHandler("getCurrencyQuote", quote_func);
            MainServer.Instance.AddXmlRPCHandler("buyCurrency", buy_func);

            m_active = true;
        }

        #endregion

        #region Implementation of IMoneyModule

        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount)
        {
            return false; // Objects cant give PP Money. (in theory it's doable however, if the user is in the sim.)
        }

        public event ObjectPaid OnObjectPaid;


        // This will be the maximum amount the user
        // is able to spend due to client limitations.
        // It is set to the equivilent of US$10K
        // as this is PayPal's maximum transaction
        // size.
        //
        // This is 1 Million cents.
        public int GetBalance(IClientAPI client)
        {
            return 1000000;
        }

        public void ApplyUploadCharge(UUID agentID)
        {
            // N/A
        }

        public bool UploadCovered(IClientAPI client)
        {
            return true;
        }

        public void ApplyGroupCreationCharge(UUID agentID)
        {
            // N/A
        }

        public bool GroupCreationCovered(IClientAPI client)
        {
            return true;
        }

        public bool AmountCovered(IClientAPI client, int amount)
        {
            return true;
        }

        public void ApplyCharge(UUID agentID, int amount, string text)
        {
            // N/A
        }

        


        /// <summary>
        /// Old Pre-1.2 Linden Lab Economy Data
        /// Completely irrelevant now.
        /// (hooray for 7 year old cruft!)
        /// 
        /// We should probably hard-code this
        /// into LLClientView TBH. -Adam
        /// </summary>
        /// <returns></returns>
        public EconomyData GetEconomyData()
        {
            const int ObjectCapacity = 45000;
            const int ObjectCount = 0;
            const int PriceEnergyUnit = 0;
            const int PriceGroupCreate = 0;
            const int PriceObjectClaim = 0;
            const float PriceObjectRent = 0f;
            const float PriceObjectScaleFactor = 0f;
            const int PriceParcelClaim = 0;
            const float PriceParcelClaimFactor = 0f;
            const int PriceParcelRent = 0;
            const int PricePublicObjectDecay = 0;
            const int PricePublicObjectDelete = 0;
            const int PriceRentLight = 0;
            const int PriceUpload = 0;
            const int TeleportMinPrice = 0;

            EconomyData edata = new EconomyData();
            edata.ObjectCapacity = ObjectCapacity;
            edata.ObjectCount = ObjectCount;
            edata.PriceEnergyUnit = PriceEnergyUnit;
            edata.PriceGroupCreate = PriceGroupCreate;
            edata.PriceObjectClaim = PriceObjectClaim;
            edata.PriceObjectRent = PriceObjectRent;
            edata.PriceObjectScaleFactor = PriceObjectScaleFactor;
            edata.PriceParcelClaim = PriceParcelClaim;
            edata.PriceParcelClaimFactor = PriceParcelClaimFactor;
            edata.PriceParcelRent = PriceParcelRent;
            edata.PricePublicObjectDecay = PricePublicObjectDecay;
            edata.PricePublicObjectDelete = PricePublicObjectDelete;
            edata.PriceRentLight = PriceRentLight;
            edata.PriceUpload = PriceUpload;
            edata.TeleportMinPrice = TeleportMinPrice;
            return edata;
        }

        #endregion

        #region Some Quick Funcs

        public XmlRpcResponse quote_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Hashtable requestData = (Hashtable) request.Params[0];
            // UUID agentId = UUID.Zero;
            int amount = 0;
            Hashtable quoteResponse = new Hashtable();
            XmlRpcResponse returnval = new XmlRpcResponse();


            Hashtable currencyResponse = new Hashtable();
            currencyResponse.Add("estimatedCost", 0);
            currencyResponse.Add("currencyBuy", amount);

            quoteResponse.Add("success", true);
            quoteResponse.Add("currency", currencyResponse);
            quoteResponse.Add("confirm", "asdfad9fj39ma9fj");

            returnval.Value = quoteResponse;
            return returnval;



        }

        public XmlRpcResponse buy_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Hashtable requestData = (Hashtable) request.Params[0];
            // UUID agentId = UUID.Zero;
            // int amount = 0;

            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable returnresp = new Hashtable();
            returnresp.Add("success", true);
            returnval.Value = returnresp;
            return returnval;
        }

        #endregion 

    }
}
