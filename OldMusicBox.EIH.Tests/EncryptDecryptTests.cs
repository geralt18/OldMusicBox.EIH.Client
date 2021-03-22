﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OldMusicBox.EIH.Client.Model;
using System.Security.Claims;
using OldMusicBox.EIH.Client.Signature;
using OldMusicBox.EIH.Client;

namespace OldMusicBox.EIH.Tests
{
    [TestClass]
    public class EncryptDecryptTests
    {
        /// <summary>
        /// Encryption and decryption
        /// </summary>
        /// <remarks>
        /// This test uses the same configuration of certificates for both the server and the client
        /// This is unusual, in normal circumstances client/server don't have access 
        /// to private keys when not necessary:
        /// * only server has private keys to sign SAML 
        /// * only client has private keys to decrypt assertions
        /// </remarks>
        [TestMethod]
        public void EncryptDecryptTest()
        {
            string name   = "foo";
            string issuer = "foo.bar.qux";

            // arrange
            var certificate = ClientCertificateProvider.GetEncCertificate();
            var principal =
                new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.Name, name),
                            new Claim(ClaimTypes.GivenName, "joe"),
                            new Claim(ClaimTypes.Surname, "doe")
                        }
                    ));

            var artifactResponseFactory   = new ArtifactResponseFactory();
            var responseFactory           = new ResponseFactory();
            var encryptedAssertionFactory = new EncryptedAssertionFactory();

            var saml2module = new Saml2AuthenticationModule();

            var x509Configuration = new X509Configuration()
            {
                SignatureCertificate = ClientCertificateProvider.GetEncCertificate(),
                SignaturePrivateKey = ClientCertificateProvider.GetEncPrivateKey(),
                IncludeKeyInfo = true,
                SignatureAlgorithm = SignatureAlgorithm.ECDSA256,
                EncryptionCertificate = ClientCertificateProvider.GetEncCertificate(),
                EncryptionPrivateKey = ClientCertificateProvider.GetEncPrivateKey()
            };

            var tokenHandler = new Saml2SecurityTokenHandler();

            responseFactory.X509Configuration = x509Configuration;
            responseFactory.InResponseTo      = Guid.NewGuid().ToString();
            responseFactory.Issuer            = issuer;

            artifactResponseFactory.X509Configuration = x509Configuration;
            artifactResponseFactory.InResponseTo      = Guid.NewGuid().ToString();
            artifactResponseFactory.Issuer            = issuer;

            // data to be encrypted
            encryptedAssertionFactory.Principal      = principal;
            encryptedAssertionFactory.IssuerDomain   = "issuer.domain.com";
            encryptedAssertionFactory.ConsumerDomain = "consumer.domain.com";
            encryptedAssertionFactory.EncryptionKey  = x509Configuration.EncryptionCertificate;

            // build the crypted assertion
            responseFactory.EncryptedAssertions               = encryptedAssertionFactory.Build();
            artifactResponseFactory.ArtifactResponse.Response = responseFactory.Build();

            // act
            var artifactResponse = artifactResponseFactory.Create();
            var saml2Token       = saml2module.ParseArtifactResponse(artifactResponse);

            // assert
            Assert.IsNotNull(saml2Token);

            saml2module.TryDecryptingEncryptedAssertions(saml2Token, x509Configuration);
            Assert.IsNotNull(saml2Token.Assertion);

            var identities = tokenHandler.ValidateToken(saml2Token);
            Assert.IsNotNull(identities);
            Assert.IsTrue(identities.Count > 0);
            var identity = identities[0];
            Assert.IsNotNull(identity);
            Assert.AreEqual(name, identity.Name);
        }
    }
}