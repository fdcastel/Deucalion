export interface FontDef {
  label: string;
  stack: string;
  italicize?: boolean;
  /**
   * Google Fonts `css2?family=` spec for faces that are not self-hosted. Only
   * the two IBM Plex families ship with the page (see styles/fonts/README.md);
   * the rest are loaded on demand, the moment a user picks them in the tweaks
   * panel, so the default page never contacts a third party.
   */
  google?: string;
}

export const DISPLAY_FONTS: Record<string, FontDef> = {
  newsreader:   { label: "Newsreader (serif)", stack: '"Newsreader", Georgia, serif', italicize: true, google: "Newsreader:ital,opsz,wght@0,6..72,300..600;1,6..72,300..600" },
  inter:        { label: "Inter (sans)",       stack: '"Inter", -apple-system, sans-serif', google: "Inter:wght@300..600" },
  ibmsans:      { label: "IBM Plex Sans",      stack: '"IBM Plex Sans", sans-serif' },
  spacegrotesk: { label: "Space Grotesk",      stack: '"Space Grotesk", sans-serif', google: "Space+Grotesk:wght@400..600" },
  jetbrains:    { label: "JetBrains Mono",     stack: '"JetBrains Mono", monospace', google: "JetBrains+Mono:wght@400..600" },
  ibmmono:      { label: "IBM Plex Mono",      stack: '"IBM Plex Mono", monospace' },
};

export const UI_FONTS: Record<string, FontDef> = {
  inter:        { label: "Inter",         stack: '"Inter", -apple-system, sans-serif', google: "Inter:wght@300..600" },
  ibmsans:      { label: "IBM Plex Sans", stack: '"IBM Plex Sans", sans-serif' },
  spacegrotesk: { label: "Space Grotesk", stack: '"Space Grotesk", sans-serif', google: "Space+Grotesk:wght@400..600" },
  system:       { label: "System UI",     stack: '-apple-system, BlinkMacSystemFont, "Segoe UI", system-ui, sans-serif' },
};

export const MONO_FONTS: Record<string, FontDef> = {
  jetbrains: { label: "JetBrains Mono", stack: '"JetBrains Mono", ui-monospace, monospace', google: "JetBrains+Mono:wght@400..600" },
  ibmmono:   { label: "IBM Plex Mono",  stack: '"IBM Plex Mono", ui-monospace, monospace' },
  spacemono: { label: "Space Mono",     stack: '"Space Mono", ui-monospace, monospace', google: "Space+Mono:wght@400..600" },
};

/** Element id of the on-demand Google Fonts stylesheet link. */
export const REMOTE_FONTS_LINK_ID = "dynamic-google-fonts";

/**
 * Builds the stylesheet URL for the given faces, or null when every face is
 * self-hosted (or a system stack) and nothing needs to be fetched.
 */
export const remoteFontsHref = (faces: readonly FontDef[]): string | null => {
  const specs = [...new Set(faces.map((f) => f.google).filter((g): g is string => g !== undefined))];
  if (specs.length === 0) return null;
  return `https://fonts.googleapis.com/css2?${specs.map((s) => `family=${s}`).join("&")}&display=swap`;
};
