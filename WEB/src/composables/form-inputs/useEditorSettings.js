export const getEditorConfig = ($q) => {
  const fonts = {
    arial: "Arial",
    arial_black: "Arial Black",
    comic_sans: "Comic Sans MS",
    courier_new: "Courier New",
    impact: "Impact",
    lucida_grande: "Lucida Grande",
    times_new_roman: "Times New Roman",
    verdana: "Verdana"
  };

  const toolbar = [
    [
      {
        label: $q.lang.editor.align,
        icon: $q.iconSet.editor.align,
        fixedLabel: true,
        list: "only-icons",
        options: ["left", "center", "right", "justify"]
      }
    ],
    ["bold", "italic", "strike", "underline"],
    ["token", "hr", "link", "custom_btn"],
    [
      {
        label: $q.lang.editor.formatting,
        icon: $q.iconSet.editor.formatting,
        list: "no-icons",
        options: ["p", "h1", "h2", "h3", "h4", "h5", "h6", "code"]
      },
      "removeFormat"
    ],
    ["quote", "unordered", "ordered", "outdent", "indent"],
    ["undo", "redo"],
    ["viewsource"]
  ];

  const rowToolbar = [
    [
      {
        label: $q.lang.editor.align,
        icon: $q.iconSet.editor.align,
        fixedLabel: true,
        list: "only-icons",
        options: ["left", "center", "right", "justify"]
      },
    ],
    ["bold", "italic", "strike", "underline"],
    [
      {
        label: $q.lang.editor.formatting,
        icon: $q.iconSet.editor.formatting,
        list: "no-icons",
        options: [
          "p",
          "h1",
          "h2",
          "h3",
          "h4",
          "h5",
          "h6",
          "code"
        ]
      },
      "removeFormat"
    ]
  ];

  return { fonts, toolbar, rowToolbar };
};
