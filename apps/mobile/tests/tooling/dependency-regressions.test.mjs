import test from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { generateKeyPairSync } from "node:crypto";

const require = createRequire(import.meta.url);
const braces = require("braces");
const forge = require("node-forge");
const certificates = require("@expo/code-signing-certificates");

test("braces rejects excessive parser and supplied AST depth with a bounded error", () => {
  for (const operation of [
    braces,
    braces.compile,
    braces.expand,
    braces.stringify,
  ]) {
    assert.throws(
      () => operation("{".repeat(4000) + "a,b" + "}".repeat(4000)),
      /nesting exceeds 100/,
    );
  }
  assert.throws(
    () => braces.parse("(".repeat(4000) + "a" + ")".repeat(4000)),
    /nesting exceeds 100/,
  );
  const nested = (depth) => {
    let node = { type: "text", value: "a" };
    for (let i = 0; i < depth; i++) node = { type: "root", nodes: [node] };
    return node;
  };
  for (const operation of [braces.compile, braces.expand, braces.stringify]) {
    assert.throws(() => operation(nested(200)), /nesting exceeds 100/);
  }
  assert.deepEqual(braces.expand("src/{one,two}/{a,b}.ts"), [
    "src/one/a.ts",
    "src/one/b.ts",
    "src/two/a.ts",
    "src/two/b.ts",
  ]);
  assert.deepEqual(braces.expand("{1..3}"), ["1", "2", "3"]);
  assert.deepEqual(braces.expand(String.raw`\{literal\}`), ["{literal}"]);
  assert.equal(braces.stringify(braces.parse("{a,b}")), "{a,b}");
});

test("forge rejects extra DigestAlgorithm elements while preserving valid RSA signatures and certificates", () => {
  const keys = generateKeyPairSync("rsa", {
    modulusLength: 2048,
    publicKeyEncoding: { type: "spki", format: "pem" },
    privateKeyEncoding: { type: "pkcs8", format: "pem" },
  });
  const privateKey = forge.pki.privateKeyFromPem(keys.privateKey);
  const publicKey = forge.pki.publicKeyFromPem(keys.publicKey);
  const digest = forge.md.sha256
    .create()
    .update("Pocket Quests signature regression");
  assert.equal(
    publicKey.verify(digest.digest().bytes(), privateKey.sign(digest)),
    true,
  );
  const { asn1 } = forge;
  const algorithm = asn1.create(
    asn1.Class.UNIVERSAL,
    asn1.Type.SEQUENCE,
    true,
    [
      asn1.create(
        asn1.Class.UNIVERSAL,
        asn1.Type.OID,
        false,
        asn1.oidToDer(forge.pki.oids.sha256).getBytes(),
      ),
      asn1.create(asn1.Class.UNIVERSAL, asn1.Type.NULL, false, ""),
      asn1.create(
        asn1.Class.UNIVERSAL,
        asn1.Type.OCTETSTRING,
        false,
        "unexpected padding",
      ),
    ],
  );
  const malformed = asn1.create(
    asn1.Class.UNIVERSAL,
    asn1.Type.SEQUENCE,
    true,
    [
      algorithm,
      asn1.create(
        asn1.Class.UNIVERSAL,
        asn1.Type.OCTETSTRING,
        false,
        digest.digest().bytes(),
      ),
    ],
  );
  const invalidSignature = privateKey.sign(
    asn1.toDer(malformed).getBytes(),
    "NONE",
  );
  assert.throws(
    () => publicKey.verify(digest.digest().bytes(), invalidSignature),
    /valid RSASSA/,
  );
  const certificate = forge.pki.createCertificate();
  certificate.publicKey = publicKey;
  certificate.serialNumber = "01";
  certificate.validity.notBefore = new Date("2026-01-01T00:00:00Z");
  certificate.validity.notAfter = new Date("2027-01-01T00:00:00Z");
  const attributes = [
    { name: "commonName", value: "local-regression.invalid" },
  ];
  certificate.setSubject(attributes);
  certificate.setIssuer(attributes);
  certificate.sign(privateKey, forge.md.sha256.create());
  const roundTrip = forge.pki.certificateFromPem(
    forge.pki.certificateToPem(certificate),
  );
  assert.equal(roundTrip.verify(roundTrip), true);
  const codeSigningCertificate =
    certificates.generateSelfSignedCodeSigningCertificate({
      keyPair: { privateKey, publicKey },
      validityNotBefore: new Date(Date.now() - 60000),
      validityNotAfter: new Date(Date.now() + 60000),
      commonName: "local-regression.invalid",
    });
  certificates.validateSelfSignedCertificate(codeSigningCertificate, {
    privateKey,
    publicKey,
  });
  assert.ok(
    certificates.signBufferRSASHA256AndVerify(
      privateKey,
      codeSigningCertificate,
      Buffer.from("synthetic manifest"),
    ),
  );
});
