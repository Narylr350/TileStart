// @category TileStart
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.symbol.Reference;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Set;
import java.util.TreeSet;

public class TargetDecompile extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length < 2) {
            throw new IllegalArgumentException("output path and at least one name fragment are required");
        }

        File output = new File(args[0]);
        String[] needles = Arrays.copyOfRange(args, 1, args.length);
        DecompInterface decompiler = new DecompInterface();
        decompiler.toggleCCode(true);
        decompiler.toggleSyntaxTree(true);
        decompiler.setSimplificationStyle("decompile");
        if (!decompiler.openProgram(currentProgram)) {
            throw new IllegalStateException("Decompiler could not open the current program.");
        }

        List<Function> functions = new ArrayList<>();
        FunctionIterator iterator = currentProgram.getFunctionManager().getFunctions(true);
        while (iterator.hasNext()) {
            Function function = iterator.next();
            String name = function.getName(true);
            for (String needle : needles) {
                if (name.contains(needle)) {
                    functions.add(function);
                    break;
                }
            }
        }
        functions.sort(Comparator.comparing(function -> function.getEntryPoint().toString()));

        File parent = output.getParentFile();
        if (parent != null) {
            parent.mkdirs();
        }

        try (PrintWriter writer = new PrintWriter(new OutputStreamWriter(new FileOutputStream(output), "UTF-8"))) {
            writer.println("Program: " + currentProgram.getExecutablePath());
            writer.println("ImageBase: " + currentProgram.getImageBase());
            writer.println("TargetCount: " + functions.size());
            for (Function function : functions) {
                if (monitor.isCancelled()) {
                    break;
                }

                String name = function.getName(true);
                if (name.contains("dtor$") || name.contains("catch$")) {
                    continue;
                }

                writer.println("\n================================================================================");
                writer.println("NAME: " + name);
                writer.println("ENTRY: " + function.getEntryPoint());
                writer.println("SIGNATURE: " + function.getSignature());
                writer.println("CALLERS:");
                Set<String> callers = new TreeSet<>();
                for (Reference reference : currentProgram.getReferenceManager().getReferencesTo(function.getEntryPoint())) {
                    Function caller = currentProgram.getFunctionManager().getFunctionContaining(reference.getFromAddress());
                    if (caller != null) {
                        callers.add(caller.getEntryPoint() + " " + caller.getName(true));
                    }
                }
                for (String caller : callers) {
                    writer.println("  " + caller);
                }

                writer.println("CALLEES:");
                List<String> callees = new ArrayList<>();
                for (Function callee : function.getCalledFunctions(monitor)) {
                    callees.add(callee.getEntryPoint() + " " + callee.getName(true));
                }
                Collections.sort(callees);
                for (String callee : callees) {
                    writer.println("  " + callee);
                }

                if (!function.isExternal() && !function.isThunk()) {
                    DecompileResults results = decompiler.decompileFunction(function, 240, monitor);
                    if (results.decompileCompleted() && results.getDecompiledFunction() != null) {
                        writer.println("DECOMPILE:");
                        writer.println(results.getDecompiledFunction().getC());
                    } else {
                        writer.println("DECOMPILE_ERROR: " + results.getErrorMessage());
                    }
                }
                writer.flush();
            }
        } finally {
            decompiler.dispose();
        }

        println("Wrote " + functions.size() + " matching functions to " + output);
    }
}
