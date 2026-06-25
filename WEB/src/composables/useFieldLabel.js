import { computed, unref } from "vue";

// Parses a field label that may encode "mandatory" as a trailing "*" (the app-wide convention, e.g.
// "Currency *"). Returns the clean label text plus whether the required marker should show — driven by
// either an explicit `required` flag or a trailing "*". Centralised so every field renders the new
// top-left external label + red asterisk identically.
export function useFieldLabel (label, required) {
  const raw = computed(() => unref(label) || "");
  const text = computed(() => raw.value.replace(/\s*\*\s*$/, "").trim());
  const isRequired = computed(() => !!unref(required) || /\*\s*$/.test(raw.value));
  return { text, isRequired };
}
