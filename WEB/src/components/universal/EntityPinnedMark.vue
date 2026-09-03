<template>
  <!-- Renders nothing at all on an unpinned row, so the cell reads exactly as it always did. -->
  <q-icon v-if="pinned" name="o_push_pin" size="14px" color="primary" class="uf-pinned">
    <q-tooltip>Pinned by you — it stays at the top of this page</q-tooltip>
  </q-icon>
</template>

<script setup>
// The pin a row wears once it has been pinned, shown beside the record's own identifier.
//
// It exists because the pin BUTTON is at the far right of the row and the reason the row is at the top
// is at the far left. On a list sorted by anything at all, a row that has jumped its place with nothing
// to show for it reads as a sorting bug — this is the one mark that says "you put it there".
//
// A component rather than four lines copied into each list: the icon, its size, its colour, its tilt and
// its wording have to be identical everywhere, and three copies of an icon are three chances to drift.
defineProps({
  pinned: { type: Boolean, default: false }
});
</script>

<style scoped>
/* The same lean the pin button takes when it is on, so the mark and the control that set it read as one
   thing rather than as two different pins. */
.uf-pinned {
  transform: rotate(-35deg);
  margin-right: 4px;
  vertical-align: middle;
}
</style>
