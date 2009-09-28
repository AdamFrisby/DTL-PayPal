A currency module for OpenSimulator (www.opensimulator.org)

THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY

EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED

WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE

DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;

LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT

(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS

SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

----
WARNING: USING THIS IN ANY FORM OF PRODUCTION ENVIRONMENT IS ASKING FOR TROUBLE. 
IT IS HIGHLY UNTESTED, EXPERIMENTAL AND COMPLETELY UNWARRANTIED. 
IF IT BREAKS, YOU GET TO KEEP BOTH PIECES, BUT MAY HAVE LOST YOUR WALLET.
----

This module uses PayPal as the backend, this means all transactions are real 
hard transactions in USD. By default, this module sets the rate at 
OS$1 = 1 US Cent, eg - OS$300 = US$3.00. Payments are confirmed by the buyer 
through PayPal's standard purchasing interface; the simulator is then notified 
via PayPal IPN of the payment - and will process the original request.


Configuring this module:
Add the following sections to your OpenSim.ini file

[DTL PayPal]
Enabled = true

[DTL PayPal Users]
User Name=paypal@email.com
Other User=another.paypal@email.com
...
etc
...

How the PayPal Users section is formatted:
One line per user, this should be the avatar name. The email address is the 
primary email address used for the PayPal account that their funds will be 
recieved by. Users not listed in this section will not be able to recieve 
funds (however any user can send them.)