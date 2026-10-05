const minimumLength = 15;

const commonPasswords = new Set([
  "password123456!",
  "password123456789!",
  "p@ssword123456!",
  "p@ssw0rd123456!",
  "qwerty123456789!",
  "qwertyuiop12345!",
  "welcome1234567!",
  "changeme123456!",
  "letmein1234567!",
  "admin123456789!",
  "administrator1!",
  "superadmin12345!",
  "company1234567!",
  "nvgdispatch123!",
  "nvginventory1!",
  "spring20261234!",
  "summer20261234!",
  "winter20261234!",
  "january2026123!",
  "december202612!"
]);

export type PasswordRequirements = {
  minimumLength: boolean;
  uppercase: boolean;
  number: boolean;
  specialCharacter: boolean;
  common: boolean;
};

export function getPasswordRequirements(password: string): PasswordRequirements {
  const normalized = password.trim().replace(/\s/g, "").toLowerCase();
  return {
    minimumLength: password.length >= minimumLength,
    uppercase: /[A-Z]/.test(password),
    number: /\d/.test(password),
    specialCharacter: /[^A-Za-z0-9\s]/.test(password),
    common: password.length > 0 && !commonPasswords.has(normalized)
  };
}

export function getPasswordValidationMessage(password: string): string | null {
  if (!password.trim()) return "Enter a new password.";

  const requirements = getPasswordRequirements(password);
  if (!requirements.minimumLength || !requirements.uppercase || !requirements.number || !requirements.specialCharacter) {
    return "Use at least 15 characters, including an uppercase letter, a number, and a special character.";
  }

  if (!requirements.common) return "Choose a password that is less common and easier for others to guess.";
  return null;
}

