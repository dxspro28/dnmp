build:
	mcs src/Program.cs -out:bin/dnmp -unsafe
install:
	cp bin/dnmp ~/.local/bin/dnmp &>/dev/null
