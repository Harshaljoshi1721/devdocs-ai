export interface PasswordStrength {
  score: 0 | 1 | 2 | 3 | 4;
  label: string;
}

const LABELS = ["Too short", "Weak", "Fair", "Good", "Strong"] as const;

/**
 * A lightweight, dependency-free password-strength estimate for the signup meter.
 * Below the 8-char minimum it is always the weakest; above it, points accrue for
 * length, mixed case, digits, and symbols.
 */
export function passwordStrength(password: string): PasswordStrength {
  if (password.length < 8) return { score: 0, label: LABELS[0] };

  let points = 1; // meets the minimum length
  if (password.length >= 12) points++;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) points++;
  if (/\d/.test(password)) points++;
  if (/[^A-Za-z0-9]/.test(password)) points++;

  const score = Math.min(4, points) as PasswordStrength["score"];
  return { score, label: LABELS[score] };
}
