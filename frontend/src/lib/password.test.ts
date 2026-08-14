import { describe, expect, it } from "vitest";
import { passwordStrength } from "./password";

describe("passwordStrength", () => {
  it("rates a too-short password as the weakest", () => {
    expect(passwordStrength("abc")).toMatchObject({ score: 0, label: "Too short" });
  });

  it("rates a bare 8-char lowercase password as weak", () => {
    expect(passwordStrength("aaaaaaaa").score).toBe(1);
  });

  it("climbs as length, cases, digits and symbols are added", () => {
    expect(passwordStrength("aaaaaaaaaaaa").score).toBeGreaterThanOrEqual(2); // length ≥12
    expect(passwordStrength("Abcdefgh").score).toBeGreaterThanOrEqual(2); // mixed case
    expect(passwordStrength("Abcd1234").score).toBeGreaterThanOrEqual(3); // + digit
  });

  it("rates a long mixed-class password as strong", () => {
    expect(passwordStrength("Abcd1234!xyz")).toMatchObject({ score: 4, label: "Strong" });
  });

  it("never exceeds the top score", () => {
    expect(passwordStrength("Abcd1234!@#$XYZ").score).toBeLessThanOrEqual(4);
  });

  it("treats empty as too short", () => {
    expect(passwordStrength("").score).toBe(0);
  });
});
