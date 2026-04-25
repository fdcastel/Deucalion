export interface FontDef {
  label: string;
  stack: string;
  italicize?: boolean;
}

export const DISPLAY_FONTS: Record<string, FontDef> = {
  newsreader:   { label: "Newsreader (serif)", stack: '"Newsreader", Georgia, serif', italicize: true },
  inter:        { label: "Inter (sans)",       stack: '"Inter", -apple-system, sans-serif' },
  ibmsans:      { label: "IBM Plex Sans",      stack: '"IBM Plex Sans", sans-serif' },
  spacegrotesk: { label: "Space Grotesk",      stack: '"Space Grotesk", sans-serif' },
  jetbrains:    { label: "JetBrains Mono",     stack: '"JetBrains Mono", monospace' },
  ibmmono:      { label: "IBM Plex Mono",      stack: '"IBM Plex Mono", monospace' },
};

export const UI_FONTS: Record<string, FontDef> = {
  inter:        { label: "Inter",         stack: '"Inter", -apple-system, sans-serif' },
  ibmsans:      { label: "IBM Plex Sans", stack: '"IBM Plex Sans", sans-serif' },
  spacegrotesk: { label: "Space Grotesk", stack: '"Space Grotesk", sans-serif' },
  system:       { label: "System UI",     stack: '-apple-system, BlinkMacSystemFont, "Segoe UI", system-ui, sans-serif' },
};

export const MONO_FONTS: Record<string, FontDef> = {
  jetbrains: { label: "JetBrains Mono", stack: '"JetBrains Mono", ui-monospace, monospace' },
  ibmmono:   { label: "IBM Plex Mono",  stack: '"IBM Plex Mono", ui-monospace, monospace' },
  spacemono: { label: "Space Mono",     stack: '"Space Mono", ui-monospace, monospace' },
};
