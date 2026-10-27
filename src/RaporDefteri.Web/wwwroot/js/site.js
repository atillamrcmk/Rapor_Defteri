document.addEventListener("submit", function (event) {
  var form = event.target;
  if (!(form instanceof HTMLFormElement)) return;
  if (form.dataset.submitted === "1") {
    event.preventDefault();
    return;
  }
  form.dataset.submitted = "1";
});

document.querySelectorAll("[data-person-search]").forEach(function (input) {
  var box = document.querySelector(input.getAttribute("data-person-search"));
  var list = document.querySelector(input.getAttribute("data-person-list"));
  var index = -1;
  input.addEventListener("input", function () { search(input.value); });
  input.addEventListener("keydown", function (event) {
    var items = box.querySelectorAll("li");
    if (event.key === "ArrowDown") { index = Math.min(index + 1, items.length - 1); mark(items); event.preventDefault(); }
    if (event.key === "ArrowUp") { index = Math.max(index - 1, 0); mark(items); event.preventDefault(); }
    if (event.key === "Enter" && index >= 0 && items[index]) { choose(items[index]); event.preventDefault(); }
  });

  function mark(items) {
    items.forEach(function (item, i) { item.classList.toggle("active", i === index); });
  }

  function search(query) {
    if (!query || query.trim().length < 2) { box.innerHTML = ""; return; }
    fetch("/kisiler/oner?q=" + encodeURIComponent(query))
      .then(function (response) { if (!response.ok) throw new Error("arama"); return response.json(); })
      .then(function (rows) {
        box.innerHTML = "";
        index = -1;
        rows.forEach(function (row) {
          var item = document.createElement("li");
          item.textContent = row.fileNumber + " — " + row.fullName + (row.ward ? " — " + row.ward : "") + " — " + row.presence;
          item.dataset.id = row.id;
          item.dataset.label = item.textContent;
          item.addEventListener("click", function () { choose(item); });
          box.appendChild(item);
        });
      })
      .catch(function () { box.innerHTML = "<li>Arama yapılamadı. Kayıt eklenmedi.</li>"; });
  }

  function choose(item) {
    if (list.querySelector("input[value='" + item.dataset.id + "']")) { box.innerHTML = ""; return; }
    var line = document.createElement("li");
    line.textContent = item.dataset.label;
    var hidden = document.createElement("input");
    hidden.type = "hidden";
    hidden.name = "InmateIds";
    hidden.value = item.dataset.id;
    line.appendChild(hidden);
    list.appendChild(line);
    box.innerHTML = "";
    input.value = "";
  }
});
