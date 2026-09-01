<template>
  <!-- A name as it reads: the generational particle first, in bold, then the name. The particle is the
       part a reader is looking for — a column of "John Smith" rows is told apart by the "Jr." and the
       "III" alone, and in the same weight as the name beside it that particle disappears. -->
  <span class="app-name"><span
    v-if="particle" class="app-name__suffix"
  >{{ particle }}</span>{{ tail }}</span>
</template>

<script setup>
// THE way a name with a generational suffix is drawn, anywhere in the app.
//
// It exists because the suffix has to read the same everywhere and be told apart from the name at a
// glance. The ORDER is the form's: every box that asks for one puts Suffix to the LEFT of First Name, and
// a screen showing the answers back should not reorder them. The WEIGHT is what makes it useful: two
// clients called John Smith are distinguished by the particle and nothing else, so it is the part that
// has to stand out.
//
// The name and the suffix arrive as two props, never as one joined string — a component handed
// "Jr. John Smith" cannot tell where the particle ends. Callers holding only the joined form should carry
// the suffix through to here instead; the list rows all do.
//
// clientDisplayName / addresseeName / roleAddressedName join the same two parts into plain text, in the
// same order, for the places that need a string: a table's sort and search value, an email, a
// notification. This component is for the places that render.
import { computed } from "vue";

const props = defineProps({
  // The name WITHOUT the particle.
  name: { type: String, default: "" },
  // The generational particle — Jr., Sr., III.
  suffix: { type: String, default: "" },
  // What to show when there is no name at all. A record with no name is information, so it reads as a
  // dash rather than as nothing.
  empty: { type: String, default: "—" }
});

const label = computed(() => String(props.name ?? "").trim());

// A particle with no name behind it is not a name — that renders as the empty placeholder instead.
const particle = computed(() => (label.value ? String(props.suffix ?? "").trim() : ""));

// The name, with the space that separates it from the particle carried on the front. In the text node
// rather than as a span of its own so it survives a copy-paste and needs no whitespace-preserving CSS.
const tail = computed(() => {
  if (!label.value) return particle.value ? "" : props.empty;
  return particle.value ? ` ${label.value}` : label.value;
});
</script>

<style scoped>
/* Heavier than the name beside it whatever weight that name is set in: this renders inside plain cells
   and inside already-medium headings, and in both it has to be the part that catches the eye. */
.app-name__suffix {
  font-weight: 700;
}
</style>
